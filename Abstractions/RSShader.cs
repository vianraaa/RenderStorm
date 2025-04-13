using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RenderStorm.Display;
using RenderStorm.Types;
using Vortice.D3DCompiler;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Shader;
using Vortice.Dxc;
using Vortice.DXGI;

namespace RenderStorm.Abstractions;

public class RSShader : IDisposable
{
    protected ID3D11Device _device;
    protected ID3D11VertexShader _vertexShader;
    protected ID3D11PixelShader _pixelShader;
    protected ID3D11InputLayout _inputLayout;
    protected bool _disposed;
    
    public string DebugName { get; protected set; }
    public ID3D11InputLayout InputLayout => _inputLayout;

    public RSShader(ID3D11Device device, string shaderSource, 
        InputElementDescription[] inputLayout, string debugName = "Shader")
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        DebugName = debugName;

        CompileShaders(shaderSource, inputLayout);
    }

    protected virtual void CompileShaders(string shaderSource, 
        InputElementDescription[] inputLayout)
    {
        byte[] vertexShaderBytecode = CompileShader(shaderSource, DxcShaderStage.Vertex, "VertexProc");
        byte[] pixelShaderBytecode = CompileShader(shaderSource, DxcShaderStage.Pixel, "FragmentProc");

        _vertexShader = _device.CreateVertexShader(vertexShaderBytecode);
        _pixelShader = _device.CreatePixelShader(pixelShaderBytecode);

        if (inputLayout != null && inputLayout.Length > 0)
        {
            _inputLayout = _device.CreateInputLayout(inputLayout, vertexShaderBytecode);
        }
    }

    protected byte[] CompileShader(string source, DxcShaderStage type, string entryPoint = "Main")
    {
        string profile = type == DxcShaderStage.Vertex ? "vs_5_0" : "ps_5_0";

        ShaderFlags flags = ShaderFlags.EnableStrictness;
        flags |= ShaderFlags.OptimizationLevel3;
        
        var result = Compiler.Compile(source, entryPoint , "RenderstormShaderSource", profile);
        byte[] buffer = new byte[result.Length];
        result.CopyTo(buffer);
        return buffer;
    }

    public virtual void Use(D3D11DeviceContainer container)
    {
        var context = container.Context;
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSShader));
        
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
            
        if (_inputLayout != null)
            context.IASetInputLayout(_inputLayout);
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();
            _inputLayout?.Dispose();
            _disposed = true;
        }
    }
}

public class RSShader<T> : RSShader where T : unmanaged
{
    private static readonly Dictionary<string, RSShader<T>> _shaderCache = new();
    private readonly Dictionary<string, int> _uniformLocations = new();
    private readonly Dictionary<uint, ID3D11Buffer> _constantBuffers = new();
    
    public RSShader(ID3D11Device device, string shaderSource, 
        string debugName = "Shader") : base(device, shaderSource, null, debugName)
    {
        // Cache this shader instance if caching is desired
        string cacheKey = ComputeShaderHash(shaderSource);
        if (!_shaderCache.ContainsKey(cacheKey))
        {
            _shaderCache[cacheKey] = this;
        }
    }

    protected override void CompileShaders(string shaderSource, 
        InputElementDescription[] unusedInputLayout)
    {
        byte[] vertexShaderBytecode = CompileShader(shaderSource, DxcShaderStage.Vertex, "VertexProc");
        byte[] pixelShaderBytecode = CompileShader(shaderSource, DxcShaderStage.Pixel, "FragmentProc");

        _vertexShader = _device.CreateVertexShader(vertexShaderBytecode);
        _pixelShader = _device.CreatePixelShader(pixelShaderBytecode);

        // Create input layout based on the vertex type T
        CreateInputLayoutFromType(vertexShaderBytecode);
    }

    private void CreateInputLayoutFromType(byte[] shaderBytecode)
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var layoutElements = new List<InputElementDescription>();
        uint offset = 0;

        foreach (var field in fields)
        {
            Format format;
            uint size;

            // Get semantic name from attribute if available, otherwise use field name
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
            else if (field.FieldType == typeof(float))
            {
                format = Format.R32_Float;
                size = sizeof(float);
            }
            else if (field.FieldType == typeof(int) || field.FieldType == typeof(uint))
            {
                format = Format.R32_SInt;
                size = sizeof(int);
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
            _inputLayout = _device.CreateInputLayout(layoutElements.ToArray(), shaderBytecode);
        }
    }

    public unsafe void SetUniform<TUniform>(D3D11DeviceContainer container, uint slot, TUniform value) 
        where TUniform : unmanaged
    {
        var context = container.Context;
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSShader<T>));
        
        if (!_constantBuffers.TryGetValue(slot, out var buffer))
        {
            var bufferDesc = new BufferDescription
            {
                ByteWidth = (uint)((sizeof(TUniform) + 15) & ~15), // round up to 16 bytes
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CPUAccessFlags = CpuAccessFlags.Write
            };
            
            buffer = _device.CreateBuffer(bufferDesc);
            buffer.DebugName = $"{DebugName}_CB_Register{slot}";
            _constantBuffers[slot] = buffer;
        }

        // update the buffer with new data
        var mappedResource = context.Map(buffer, 0, MapMode.WriteDiscard);
        try
        {
            *(TUniform*)mappedResource.DataPointer = value;
        }
        finally
        {
            context.Unmap(buffer, 0);
        }
        
        context.VSSetConstantBuffers(slot, new[] { buffer });
        context.PSSetConstantBuffers(slot, new[] { buffer });
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            foreach (var buffer in _constantBuffers.Values)
            {
                buffer.Dispose();
            }
            _constantBuffers.Clear();
            
            base.Dispose();
        }
    }

    private static string ComputeShaderHash(string shaderSource)
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

// Optional attribute to specify HLSL semantic names that differ from field names
[AttributeUsage(AttributeTargets.Field)]
public class SemanticNameAttribute : Attribute
{
    public string Name { get; }
    
    public SemanticNameAttribute(string name)
    {
        Name = name;
    }
}