using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RenderStorm.Display;
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
    private readonly ID3D11Device _device;
    private readonly ResourceUsage _usage;
    private readonly CpuAccessFlags _cpuAccessFlags;

    public unsafe RSBuffer(ID3D11Device device, ReadOnlySpan<T> data, BindFlags bindFlags, 
        ResourceUsage usage = ResourceUsage.Default, CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None, 
        string debugName = "Buffer")
    {
        _device = device;
        DebugName = debugName;
        BindFlags = bindFlags;
        _usage = usage;
        _cpuAccessFlags = cpuAccessFlags;
        
        ItemCount = data.Length;
        Size = data.Length * Marshal.SizeOf<T>();
        
        var bufferDesc = new BufferDescription
        {
            ByteWidth = (uint)Size,
            BindFlags = bindFlags,
            Usage = usage,
            CPUAccessFlags = cpuAccessFlags
        };

        // Create a safe copy of the data to ensure GC doesn't move it during the operation
        T[] dataCopy = data.ToArray();
        fixed (void* dataPtr = dataCopy)
        {
            var initialData = new SubresourceData
            {
                DataPointer = (IntPtr)dataPtr
            };
            
            device.CreateBuffer(bufferDesc, initialData, out _buffer);
        }
        
        if (!string.IsNullOrEmpty(debugName))
        {
            _buffer.DebugName = debugName;
        }
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
        GC.SuppressFinalize(this);
    }

    public void BindAsVertexBuffer(D3D11DeviceContainer container, uint slot = 0, uint offsetInBytes = 0)
    {
        var context = container.Context;
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.VertexBuffer))
            throw new InvalidOperationException("Buffer was not created with VertexBuffer bind flag");
            
        context.IASetVertexBuffer(slot, _buffer, (uint)Marshal.SizeOf<T>(), offsetInBytes);
    }

    public void BindAsIndexBuffer(D3D11DeviceContainer container, Format format = Format.R32_UInt, uint offsetInBytes = 0)
    {
        var context = container.Context;
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.IndexBuffer))
            throw new InvalidOperationException("Buffer was not created with IndexBuffer bind flag");
            
        // Verify format is appropriate for the type
        if (format != Format.R16_UInt && format != Format.R32_UInt)
            throw new ArgumentException("Index buffer format must be R16_UInt or R32_UInt", nameof(format));
            
        if (format == Format.R16_UInt && Marshal.SizeOf<T>() != 2)
            throw new InvalidOperationException("R16_UInt format requires a 16-bit index type");
            
        if (format == Format.R32_UInt && Marshal.SizeOf<T>() != 4)
            throw new InvalidOperationException("R32_UInt format requires a 32-bit index type");
            
        context.IASetIndexBuffer(_buffer, format, offsetInBytes);
    }

    public void BindAsConstantBuffer(D3D11DeviceContainer container, ShaderStages shaderStage, uint slot)
    {
        var context = container.Context;
        ID3D11Buffer[] buffers = [_buffer];
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.ConstantBuffer))
            throw new InvalidOperationException("Buffer was not created with ConstantBuffer bind flag");
            
        if ((shaderStage & ShaderStages.Vertex) != 0)
            context.VSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Pixel) != 0)
            context.PSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Geometry) != 0)
            context.GSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Hull) != 0)
            context.HSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Domain) != 0)
            context.DSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Compute) != 0)
            context.CSSetConstantBuffers(slot, buffers);
    }
    public ID3D11Buffer NativeBuffer => _disposed ? throw new ObjectDisposedException(nameof(RSBuffer<T>)) : _buffer;
}
[Flags]
public enum ShaderStages
{
    None = 0,
    Vertex = 1,
    Pixel = 2,
    Geometry = 4,
    Hull = 8,
    Domain = 16,
    Compute = 32,
    All = Vertex | Pixel | Geometry | Hull | Domain | Compute
}