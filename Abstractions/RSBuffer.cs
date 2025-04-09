using System;
using System.Runtime.InteropServices;
using RenderStorm.Other;
using Silk.NET.OpenGL;

namespace RenderStorm.Abstractions;

public interface ITypedBuffer
{
    public Type StoredType { get; }
    public int ItemCount { get; }
    public int Size { get; }
    public BufferTargetARB Target { get; }
}

public class RSBuffer<T> : IProfilerObject, ITypedBuffer, IDisposable where T : unmanaged
{
    private bool _disposed;

    public RSBuffer(BufferTargetARB target, ReadOnlySpan<T> data, BufferUsageARB usage = BufferUsageARB.StaticDraw,
        string debugName = "Buffer")
    {
        DebugName = debugName;
        Target = target;
        NativeInstance = OpenGL.API.GenBuffer();
        Bind();
        OpenGL.API.BufferData(Target, data, usage);
        ItemCount = data.Length;
        Size = data.Length * Marshal.SizeOf<T>();
        RSDebugger.BufferCount++;
        RSDebugger.Buffers.Add(this);
    }

    public uint Handle => NativeInstance;

    public void Dispose()
    {
        if (!_disposed)
        {
            RSDebugger.BufferCount--;
            RSDebugger.Buffers.Remove(this);
            OpenGL.API.DeleteBuffer(NativeInstance);
            _disposed = true;
        }
    }

    public int ItemCount { get; private set; }
    public int Size { get; private set; }
    public BufferTargetARB Target { get; }

    public Type StoredType => typeof(T);

    public void Bind()
    {
        OpenGL.API.BindBuffer(Target, NativeInstance);
    }

    public void Unbind()
    {
        OpenGL.API.BindBuffer(Target, 0);
    }

    public void UpdateData(ReadOnlySpan<T> data, int offset = 0)
    {
        Bind();
        var size = (nuint)(data.Length * Marshal.SizeOf<T>());
        Size = data.Length * Marshal.SizeOf<T>();
        ItemCount = data.Length;
        unsafe
        {
            fixed (void* ptr = data)
            {
                OpenGL.API.BufferSubData(Target, offset, size, ptr);
            }
        }
    }
}