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
    
    private void CreateInputLayoutFromType(byte[] shaderBytecode, Type type)
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
                _inputLayout = D3D11DeviceContainer.SharedState.Device.CreateInputLayout(layoutElements.ToArray(), shaderBytecode);
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

    public RSShader(string shaderSource, 
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
            _vertexShader = D3D11DeviceContainer.SharedState.Device.CreateVertexShader(vertexBytes);
            _pixelShader = D3D11DeviceContainer.SharedState.Device.CreatePixelShader(pixelBytes);
            CreateInputLayoutFromType(vertexBytes, vertex);
            RSDebugger.Shaders.Add(this);
            return;
        }

        CompileShaders(shaderSource, vertex);
        RSDebugger.Shaders.Add(this);
    }
    
    public RSShader(byte[] vertexBytes, byte[] pixelBytes,
        Type vertex, string debugName = "Shader")
    {
        DebugName = debugName;

        _vertexShader = D3D11DeviceContainer.SharedState.Device.CreateVertexShader(vertexBytes);
        _pixelShader = D3D11DeviceContainer.SharedState.Device.CreatePixelShader(pixelBytes);

        CreateInputLayoutFromType(vertexBytes, vertex);
        RSDebugger.Shaders.Add(this);
    }

    public void SetResource(uint register, ID3D11ShaderResourceView view)
    {
        D3D11DeviceContainer.SharedState.Context.PSSetShaderResource(register, view);
    }
    public void SetSampler(uint register, ID3D11SamplerState sampler)
    {
        D3D11DeviceContainer.SharedState.Context.PSSetSampler(register, sampler);
    }

    public void SetTexture(uint register, RSTexture texture)
    {
        D3D11DeviceContainer.SharedState.Context.PSSetShaderResource(register, texture.ShaderResourceView);
        D3D11DeviceContainer.SharedState.Context.PSSetSampler(register, texture.SamplerState);
    }

    protected void CompileShaders(string shaderSource, Type vertex)
    {
        var dev = D3D11DeviceContainer.SharedState.Device;
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
            CreateInputLayoutFromType(vertexShaderBytecode, vertex);
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

    public unsafe void SetCBuffer<TUniform>(uint slot, TUniform value)
    {
        using (new TracyWrapper.ProfileScope("Set Constant Buffer", ZoneC.DARK_ORANGE))
        {
            var context = D3D11DeviceContainer.SharedState.Context;
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
                
                buffer = D3D11DeviceContainer.SharedState.Device.CreateBuffer(bufferDesc);
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

    public void Use()
    {
        using (new TracyWrapper.ProfileScope("Use Shader", ZoneC.DARK_ORANGE))
        {
            D3D11DeviceContainer.SharedState.Context.IASetInputLayout(_inputLayout);
            D3D11DeviceContainer.SharedState.Context.VSSetShader(_vertexShader);
            D3D11DeviceContainer.SharedState.Context.PSSetShader(_pixelShader);
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
    public RSShader(string shaderSource,
        string debugName = "Shader") : base(shaderSource, typeof(T), debugName) { }
    
    public RSShader(byte[] vertexBytes, byte[] pixelBytes,
        string debugName = "Shader") : base(vertexBytes, pixelBytes, typeof(T), debugName) { }
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