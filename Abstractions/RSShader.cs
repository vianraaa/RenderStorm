using System.Numerics;
using System.Runtime.InteropServices;
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

    public RSShader(string vertexShaderSource, string fragmentShaderSource, string debugName = "Shader")
    {
        _shaders.Add(this);
        DebugName = debugName;
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