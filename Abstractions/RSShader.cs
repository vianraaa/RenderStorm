using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using RenderStorm.Display;
using RenderStorm.Types;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Shader;
using Vortice.Dxc;
using Vortice.DXGI;

namespace RenderStorm.Abstractions;

public class RSShader: IDisposable
{
    protected ID3D11Device _device;
    protected ID3D11DeviceContext _context;
    protected ID3D11VertexShader _vertexShader;
    protected ID3D11PixelShader _pixelShader;
    public ID3D11InputLayout InputLayout;
    protected ID3D11Buffer _constantBuffer;
    public virtual void Dispose()
    {
        
    }
    public void Use()
    {
        _context.IASetInputLayout(InputLayout);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_pixelShader);
    }
}

public class RSShader<T> : RSShader
{
    private bool _disposed;
    private static List<RSShader> _shaders = new List<RSShader>();
    private Dictionary<string, int> _uniforms = new();
    
    public string DebugName { get; private set; }

    internal static void Shutdown()
    {
        foreach (var shader in _shaders.ToArray())
        {
            shader.Dispose();
        }
        _shaders.Clear();
    }
    
    static string HashStr(string rawData)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }

    public RSShader(ID3D11Device device, ID3D11DeviceContext context, string vertexShaderSource, string pixelShaderSource, string debugName = "Shader")
    {
        _device = device;
        _context = context;
        DebugName = debugName;
        _shaders.Add(this);

        string hash1 = HashStr(vertexShaderSource);
        string hash2 = HashStr(pixelShaderSource);
        string hash3 = HashStr(hash1 + hash2);
        string cachePath = Path.Combine(RSWindow.Instance.CachePath, hash3);
            
        if (File.Exists(cachePath))
        {
            byte[] binary = File.ReadAllBytes(cachePath);
            _vertexShader = _device.CreateVertexShader(binary);
            _pixelShader = _device.CreatePixelShader(binary);
            CreateInputLayout(binary);
            return;
        }

        CompileAndCacheShaders(vertexShaderSource, pixelShaderSource, cachePath);
    }
    
    private void CompileAndCacheShaders(string vertexShaderSource, string pixelShaderSource, string cachePath)
    {
        var vertexShaderBytecode = CompileShader(vertexShaderSource, DxcShaderStage.Vertex);
        var pixelShaderBytecode = CompileShader(pixelShaderSource, DxcShaderStage.Pixel);

        _vertexShader = _device.CreateVertexShader(vertexShaderBytecode);
        _pixelShader = _device.CreatePixelShader(pixelShaderBytecode);

        CreateInputLayout(vertexShaderBytecode);

        var binaryData = new byte[vertexShaderBytecode.Length + pixelShaderBytecode.Length];
        Array.Copy(vertexShaderBytecode, 0, binaryData, 0, vertexShaderBytecode.Length);
        Array.Copy(pixelShaderBytecode, 0, binaryData, vertexShaderBytecode.Length, pixelShaderBytecode.Length);

        using (var writer = new BinaryWriter(File.Open(cachePath, FileMode.Create)))
        {
            writer.Write(binaryData);
        }
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            _shaders.Remove(this);
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();
            InputLayout?.Dispose();
            _constantBuffer?.Dispose();
            _disposed = true;
        }
    }

    private byte[] CompileShader(string source, DxcShaderStage type)
    {
        string profile = type == DxcShaderStage.Vertex ? "vs_5_0" : "ps_5_0";

        ShaderFlags flags = ShaderFlags.EnableStrictness;
        flags |= ShaderFlags.OptimizationLevel3;
        var result = Compiler.Compile(source, "Main", "RenderstormShaderSource", profile);
        byte[] buffer = new byte[result.Length];
        result.CopyTo(buffer);
        return buffer;
    }
    
    private void CreateInputLayout(byte[] shaderBytecode)
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var layoutElements = new InputElementDescription[fields.Length];
        uint offset = 0;
        var attributeLocation = 0;

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Vec3))
            {
                layoutElements[attributeLocation] = new InputElementDescription(field.Name, 0, Format.R32G32B32_Float, offset, 0);
                offset += 3 * sizeof(float);
                attributeLocation++;
            }
            else if (field.FieldType == typeof(Vec2))
            {
                layoutElements[attributeLocation] = new InputElementDescription(field.Name, 0, Format.R32G32_Float, offset, 0);
                offset += 2 * sizeof(float);
                attributeLocation++;
            }
        }
        
        InputLayout = _device.CreateInputLayout(layoutElements, shaderBytecode);
    }

    public void Use()
    {
        _context.IASetInputLayout(InputLayout);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_pixelShader);
    }
    public int GetUniformLocation(string name)
    {
        if (_uniforms.ContainsKey(name))
            return _uniforms[name];
        return -1;
    }
}