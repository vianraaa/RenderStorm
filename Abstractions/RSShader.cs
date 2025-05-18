using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RenderStorm.Display;
using RenderStorm.Other;
using Vortice.D3DCompiler;
using Vortice.Direct3D11;
using Vortice.DXGI;
using TracyWrapper;

namespace RenderStorm.Abstractions;

public class RSShader : IProfilerObject, IDisposable
{
    private readonly Dictionary<string, int> _uniformLocations = new();
    private readonly Dictionary<uint, ID3D11Buffer> _constantBuffers = new();
    protected ID3D11VertexShader _vertexShader;
    protected ID3D11PixelShader _pixelShader;
    protected ID3D11InputLayout _inputLayout;
    protected bool _disposed;
    public ID3D11InputLayout InputLayout => _inputLayout;
    
    private void CreateInputLayoutFromType(ID3D11Device dev, byte[] shaderBytecode, Type type)
    {
        using (new TracyWrapper.ProfileScope("Create Input Layout", ZoneC.DARK_SLATE_GRAY))
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var layoutElements = new List<InputElementDescription>();
            uint offset = 0;

            foreach (var field in fields)
            {
                Format format;
                uint size;

                string semanticName = field.Name;
                var semanticAttrs = field.GetCustomAttributes(typeof(SemanticNameAttribute), false);
                if (semanticAttrs.Length > 0)
                {
                    semanticName = ((SemanticNameAttribute)semanticAttrs[0]).Name;
                }
                

                if (field.FieldType == typeof(Vector3))
                {
                    format = Format.R32G32B32_Float;
                    size = 3 * sizeof(float);
                }
                else if (field.FieldType == typeof(Vector2))
                {
                    format = Format.R32G32_Float;
                    size = 2 * sizeof(float);
                }
                else if (field.FieldType == typeof(Vector4))
                {
                    format = Format.R32G32B32A32_Float;
                    size = 4 * sizeof(float);
                }
                else
                {
                    continue;
                }
                

                layoutElements.Add(new InputElementDescription(semanticName, 0, format, offset, 0));
                offset += size;
            }

            
            if (layoutElements.Count > 0)
            {
                _inputLayout = dev.CreateInputLayout(layoutElements.ToArray(), shaderBytecode);
            }
        }
    }
    
    private static string ComputeShaderHash(string shaderSource)
    {
        using (new TracyWrapper.ProfileScope("Compute Shader Hash", ZoneC.DARK_SLATE_GRAY))
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(shaderSource));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }

    public RSShader(ID3D11Device device, string shaderSource, 
        Type vertex, string debugName = "Shader")
    {
        DebugName = debugName;
        
        string cachePath = Path.Combine(RSWindow.Instance.CachePath, ComputeShaderHash(shaderSource));
        string fragCache = cachePath + ".fsh";
        string vertexCache = cachePath + ".vsh";
        if (File.Exists(fragCache) && File.Exists(vertexCache))
        {
            var pixelBytes = File.ReadAllBytes(fragCache);
            var vertexBytes = File.ReadAllBytes(vertexCache);
            _vertexShader = device.CreateVertexShader(vertexBytes);
            _pixelShader = device.CreatePixelShader(pixelBytes);
            CreateInputLayoutFromType(device, vertexBytes, vertex);
            RSDebugger.Shaders.Add(this);
            return;
        }

        CompileShaders(device, shaderSource, vertex);
        RSDebugger.Shaders.Add(this);
    }
    
    public RSShader(ID3D11Device device, byte[] vertexBytes, byte[] pixelBytes,
        Type vertex, string debugName = "Shader")
    {
        DebugName = debugName;

        _vertexShader = device.CreateVertexShader(vertexBytes);
        _pixelShader = device.CreatePixelShader(pixelBytes);

        CreateInputLayoutFromType(device, vertexBytes, vertex);
        RSDebugger.Shaders.Add(this);
    }

    public void SetResource(D3D11DeviceContainer container, uint register, ID3D11ShaderResourceView view)
    {
        container.Context.PSSetShaderResource(register, view);
    }
    public void SetSampler(D3D11DeviceContainer container, uint register, ID3D11SamplerState sampler)
    {
        container.Context.PSSetSampler(register, sampler);
    }

    public void SetTexture(D3D11DeviceContainer container, uint register, RSTexture texture)
    {
        container.Context.PSSetShaderResource(register, texture.ShaderResourceView);
        container.Context.PSSetSampler(register, texture.SamplerState);
    }

    protected void CompileShaders(ID3D11Device dev, string shaderSource, Type vertex)
    {
        using (new TracyWrapper.ProfileScope("Compile Shaders", ZoneC.DARK_SLATE_BLUE))
        {
            byte[] vertexShaderBytecode = CompileShader(shaderSource, "vs_5_0", "vert");
            byte[] pixelShaderBytecode = CompileShader(shaderSource, "ps_5_0", "frag");
            
            string cachePath = Path.Combine(RSWindow.Instance.CachePath, ComputeShaderHash(shaderSource));
            string fragCache = cachePath + ".fsh";
            string vertexCache = cachePath + ".vsh";
            
            File.WriteAllBytes(fragCache, pixelShaderBytecode);
            File.WriteAllBytes(vertexCache, vertexShaderBytecode);

            _vertexShader = dev.CreateVertexShader(vertexShaderBytecode);
            _pixelShader = dev.CreatePixelShader(pixelShaderBytecode);
            CreateInputLayoutFromType(dev, vertexShaderBytecode, vertex);
        }
    }
    /// "vs_5_0" : "ps_5_0"
    private byte[] CompileShader(string source, string profile, string entryPoint = "Main")
    {
        using (new TracyWrapper.ProfileScope("Compile Shader", ZoneC.DARK_SLATE_GRAY))
        {
            ShaderFlags flags = ShaderFlags.EnableStrictness;
            flags |= ShaderFlags.OptimizationLevel3;
            var result = Compiler.Compile(source, entryPoint , "RenderstormShaderSource", profile);
            byte[] buffer = new byte[result.Length];
            result.CopyTo(buffer);
            return buffer;
        }
    }

    public unsafe void SetCBuffer<TUniform>(D3D11DeviceContainer container, uint slot, TUniform value)
    {
        using (new TracyWrapper.ProfileScope("Set Constant Buffer", ZoneC.DARK_ORANGE))
        {
            var context = container.Context;
            if (_disposed)
                throw new ObjectDisposedException(nameof(RSShader));
            
            if (!_constantBuffers.TryGetValue(slot, out var buffer))
            {
                var bufferDesc = new BufferDescription
                {
                    ByteWidth = (uint)((sizeof(TUniform) + 15) & ~15), // round up to 16 bytes
                    Usage = ResourceUsage.Dynamic,
                    BindFlags = BindFlags.ConstantBuffer,
                    CPUAccessFlags = CpuAccessFlags.Write
                };
                
                buffer = container.Device.CreateBuffer(bufferDesc);
                buffer.DebugName = $"{DebugName}_CB_Register{slot}";
                _constantBuffers[slot] = buffer;
            }
            
            var mappedResource = context.Map(buffer, 0, MapMode.WriteDiscard);
            try
            {
                *(TUniform*)mappedResource.DataPointer = value;
            }
            finally
            {
                context.Unmap(buffer, 0);
            }
            
            context.VSSetConstantBuffer(slot, buffer);
            context.PSSetConstantBuffer(slot, buffer);
        }
    }

    public void Use(D3D11DeviceContainer context)
    {
        using (new TracyWrapper.ProfileScope("Use Shader", ZoneC.DARK_ORANGE))
        {
            context.Context.IASetInputLayout(_inputLayout);
            context.Context.VSSetShader(_vertexShader);
            context.Context.PSSetShader(_pixelShader);
        }
    }

    public void Dispose()
    {
        using (new TracyWrapper.ProfileScope("RSShader Dispose", ZoneC.DARK_SLATE_BLUE))
        {
            if (!_disposed)
            {
                foreach (var buffer in _constantBuffers.Values)
                {
                    buffer.Dispose();
                }
                _constantBuffers.Clear();
                _vertexShader.Dispose();
                _pixelShader.Dispose();
                _inputLayout.Dispose();
            }
        }
    }
}

public class RSShader<T> : RSShader where T : unmanaged
{
    public RSShader(ID3D11Device device, string shaderSource,
        string debugName = "Shader") : base(device, shaderSource, typeof(T), debugName) { }
    
    public RSShader(ID3D11Device device, byte[] vertexBytes, byte[] pixelBytes,
        string debugName = "Shader") : base(device, vertexBytes, pixelBytes, typeof(T), debugName) { }
}

[AttributeUsage(AttributeTargets.Field)]
public class SemanticNameAttribute : Attribute
{
    public string Name { get; }
    
    public SemanticNameAttribute(string name)
    {
        Name = name;
    }
}