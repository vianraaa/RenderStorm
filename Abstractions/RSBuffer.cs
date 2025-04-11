using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace RenderStorm.Abstractions;

public interface ITypedBuffer
{
    public Type StoredType { get; }
    public int ItemCount { get; }
    public int Size { get; }
}

public class RSBuffer<T> : ITypedBuffer, IDisposable where T : unmanaged
{
    private bool _disposed;
    private ID3D11Buffer _buffer;

    public unsafe RSBuffer(ID3D11Device device, ReadOnlySpan<T> data, BindFlags bindFlags, string debugName = "Buffer")
    {
        DebugName = debugName;
        BindFlags = bindFlags;
            
        var bufferDesc = new BufferDescription
        {
            ByteWidth = (uint)(data.Length * Marshal.SizeOf<T>()),
            BindFlags = bindFlags,
            Usage = ResourceUsage.Default,
            CPUAccessFlags = CpuAccessFlags.None
        };

        var initialData = new SubresourceData
        {
            DataPointer = (IntPtr)Unsafe.AsPointer(ref data)
        };
            
        device.CreateBuffer(bufferDesc, initialData, out _buffer);
            
        ItemCount = data.Length;
        Size = data.Length * Marshal.SizeOf<T>();
    }

    public string DebugName { get; set; }
    public BindFlags BindFlags { get; }
    public int ItemCount { get; private set; }
    public int Size { get; private set; }
    public Type StoredType => typeof(T);

    public void Dispose()
    {
        if (!_disposed)
        {
            _buffer?.Dispose();
            _disposed = true;
        }
    }

    public void Bind(ID3D11DeviceContext context)
    {
        if (BindFlags.HasFlag(BindFlags.VertexBuffer))
        {
            context.IASetVertexBuffers(0, new[] { _buffer }, new[] { (uint)Marshal.SizeOf<T>() }, [0]);
        }
        else if (BindFlags.HasFlag(BindFlags.IndexBuffer))
        {
            context.IASetIndexBuffer(_buffer, Format.R32_UInt, 0);
        }
    }


    public void Unbind(ID3D11DeviceContext context)
    {
        if(BindFlags.HasFlag(BindFlags.VertexBuffer))
            context.IASetVertexBuffers(0, [null], new uint[1], new uint[1]);
        else if(BindFlags.HasFlag(BindFlags.IndexBuffer))
            context.IASetIndexBuffer(null, Format.R32_UInt, (uint)Marshal.SizeOf<T>());
    }
}