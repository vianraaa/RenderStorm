using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using RenderStorm.Display;
using RenderStorm.Other;
using Silk.NET.OpenGL;

namespace RenderStorm.Abstractions;

public class RSShader : IProfilerObject, IDisposable
{
    private bool _disposed;
    private static List<RSShader> _shaders = new List<RSShader>();

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

    public RSShader(string vertexShaderSource, string fragmentShaderSource, string debugName = "Shader")
    {
        _shaders.Add(this);
        DebugName = debugName;
        // can we find already existing cache?
        string hash1 = HashStr(vertexShaderSource);
        string hash2 = HashStr(fragmentShaderSource);
        string hash3 = HashStr(hash1 + hash2);
        string CachePath = Path.Combine(RSWindow.Instance.CachePath, hash3);
        if (File.Exists(CachePath))
        {
            NativeInstance = OpenGL.API.CreateProgram();
            byte[] binary = File.ReadAllBytes(CachePath);
            int formatInt = BitConverter.ToInt32(binary, 0);
            GLEnum format = (GLEnum)formatInt;
            binary = binary.Skip(4).ToArray();
            OpenGL.API.ProgramBinary(NativeInstance, format, MemoryMarshal.CreateReadOnlySpan<byte>(ref binary[0], binary.Length), (uint)binary.Length);
            OpenGL.API.GetProgram(NativeInstance, ProgramPropertyARB.LinkStatus, out int rstatus);
            if (rstatus == 0)
            {
                goto Continue;
            }

            return;
        }
        Continue:
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        NativeInstance = OpenGL.API.CreateProgram();
        OpenGL.API.AttachShader(NativeInstance, vertexShader);
        OpenGL.API.AttachShader(NativeInstance, fragmentShader);
        OpenGL.API.LinkProgram(NativeInstance);

        OpenGL.API.GetProgram(NativeInstance, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var infoLog = OpenGL.API.GetProgramInfoLog(NativeInstance);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }
        OpenGL.API.GetProgram(NativeInstance, ProgramPropertyARB.ProgramBinaryLength, out int length);
        GLEnum bformat;
        byte[] bbinary = new byte[length];
        unsafe
        {
            fixed (byte* binaryPtr = bbinary)
            {
                OpenGL.API.GetProgramBinary(
                    NativeInstance,
                    (uint)length,
                    out uint actualLength,
                    out bformat,
                    binaryPtr
                );
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Open(CachePath, FileMode.Create)))
        {
            writer.Write((int)bformat);
            writer.Write(bbinary);
        }

        OpenGL.API.DeleteShader(vertexShader);
        OpenGL.API.DeleteShader(fragmentShader);
        RSDebugger.ShaderCount++;
        RSDebugger.Shaders.Add(this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _shaders.Remove(this);
            RSDebugger.Shaders.Remove(this);
            RSDebugger.ShaderCount--;
            OpenGL.API.DeleteProgram(NativeInstance);
            _disposed = true;
        }
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        var shader = OpenGL.API.CreateShader(shaderType);
        OpenGL.API.ShaderSource(shader, source);
        OpenGL.API.CompileShader(shader);

        OpenGL.API.GetShader(shader, ShaderParameterName.CompileStatus, out var compileStatus);
        if (compileStatus == 0)
        {
            var infoLog = OpenGL.API.GetShaderInfoLog(shader);
            throw new Exception($"Shader compilation failed ({shaderType}): {infoLog}");
        }

        return shader;
    }

    public void Use()
    {
        OpenGL.API.UseProgram(NativeInstance);
    }

    public void SetUniform(string name, float value)
    {
        var location = OpenGL.API.GetUniformLocation(NativeInstance, name);
        if (location != -1)
            OpenGL.API.Uniform1(location, value);
        else
            RSDebugging.Log($"Uniform {name} not found in shader.", LogType.LOG_WARN);
    }

    public void SetUniform(string name, Vector2 value)
    {
        var location = OpenGL.API.GetUniformLocation(NativeInstance, name);
        if (location != -1)
            OpenGL.API.Uniform2(location, value);
        else
            RSDebugging.Log($"Uniform {name} not found in shader.", LogType.LOG_WARN);
    }
    
    public void SetUniform(string name, Vector3 value)
    {
        var location = OpenGL.API.GetUniformLocation(NativeInstance, name);
        if (location != -1)
            OpenGL.API.Uniform3(location, value);
        else
            RSDebugging.Log($"Uniform {name} not found in shader.", LogType.LOG_WARN);
    }
    
    public void SetUniform(string name, Matrix4x4 value)
    {
        var location = OpenGL.API.GetUniformLocation(NativeInstance, name);
        if (location != -1)
            OpenGL.API.UniformMatrix4(location, false, MemoryMarshal.CreateReadOnlySpan(ref value.M11, 16));
        else
            RSDebugging.Log($"Uniform {name} not found in shader.", LogType.LOG_WARN);
    }
}