using System;
using System.Reflection;
using System.Runtime.InteropServices;
using RenderStorm.Other;
using RenderStorm.Types;
using Silk.NET.OpenGL;

namespace RenderStorm.Abstractions;

public class RSVertexArray<T> : IProfilerObject, IDrawableArray, IDisposable where T : unmanaged
{
    private bool _disposed;
    private readonly RSBuffer<uint>? _indexBuffer;
    private readonly RSBuffer<T>? _vertexBuffer;
    public string VertexBufferName => $"{_vertexBuffer.DebugName}({_vertexBuffer.NativeInstance})";
    public string IndexBufferName => $"{_indexBuffer.DebugName}({_indexBuffer.NativeInstance})";
    public int VertexBufferIndex => (int)_vertexBuffer.NativeInstance;
    public int IndexBufferIndex => (int)_indexBuffer.NativeInstance;

    public RSVertexArray(ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices, string debugName = "VertexArray")
    {
        DebugName = debugName;
        _vertexBuffer = new RSBuffer<T>(BufferTargetARB.ArrayBuffer, vertices);
        _indexBuffer = new RSBuffer<uint>(BufferTargetARB.ElementArrayBuffer, indices);

        NativeInstance = OpenGL.API.GenVertexArray();
        Bind();
        EnableVertexAttributes();
        Unbind();
        RSDebugger.VertexArrayCount++;
        RSDebugger.VertexArrays.Add(this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            RSDebugger.VertexArrayCount--;
            RSDebugger.VertexArrays.Remove(this);
            _indexBuffer?.Dispose();
            _vertexBuffer?.Dispose();
            OpenGL.API.DeleteVertexArray(NativeInstance);
            _disposed = true;
        }
    }

    public void Bind()
    {
        OpenGL.API.BindVertexArray(NativeInstance);
        _vertexBuffer?.Bind();
        _indexBuffer?.Bind();
    }

    public unsafe void DrawIndexed()
    {
        Bind();
        OpenGL.API.DrawElements(PrimitiveType.Triangles, (uint)_indexBuffer.ItemCount, DrawElementsType.UnsignedInt,
            (void*)0);
        Unbind();
    }

    public void Unbind()
    {
        OpenGL.API.BindVertexArray(0);
    }

    private void EnableVertexAttributes()
    {
        if (_vertexBuffer == null) return;

        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var attributeLocation = 0;
        var offset = 0;

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Vec3))
            {
                var numElements = 3;
                OpenGL.API.VertexAttribPointer((uint)attributeLocation, numElements, VertexAttribPointerType.Float,
                    false, (uint)Marshal.SizeOf<T>(), offset);
                OpenGL.API.EnableVertexAttribArray((uint)attributeLocation);
                offset += numElements * sizeof(float);
                attributeLocation++;
            }
            else if (field.FieldType == typeof(Vec2))
            {
                var numElements = 2;
                OpenGL.API.VertexAttribPointer((uint)attributeLocation, numElements, VertexAttribPointerType.Float,
                    false, (uint)Marshal.SizeOf<T>(), offset);
                OpenGL.API.EnableVertexAttribArray((uint)attributeLocation);
                offset += numElements * sizeof(float);
                attributeLocation++;
            }
        }
    }
}