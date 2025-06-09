using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RenderStorm.Display;
using RenderStorm.Other;
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

public class RSBuffer<T> : IProfilerObject, ITypedBuffer, IDisposable where T : unmanaged
{
    private bool _disposed;
    private ID3D11Buffer _buffer;
    private readonly ResourceUsage _usage;
    private readonly CpuAccessFlags _cpuAccessFlags;

    public unsafe RSBuffer(ReadOnlySpan<T> data, BindFlags bindFlags, 
        ResourceUsage usage = ResourceUsage.Default, CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None, 
        string debugName = "Buffer")
    {
        RSDebugger.Buffers.Add(this);
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
        
        T[] dataCopy = data.ToArray();
        fixed (void* dataPtr = dataCopy)
        {
            var initialData = new SubresourceData
            {
                DataPointer = (IntPtr)dataPtr
            };
            
            D3D11DeviceContainer.SharedState.Device.CreateBuffer(bufferDesc, initialData, out _buffer);
        }
        
        if (!string.IsNullOrEmpty(debugName))
        {
            _buffer.DebugName = debugName;
        }
    }
    
    public BindFlags BindFlags { get; }
    public int ItemCount { get; private set; }
    public int Size { get; private set; }
    public Type StoredType => typeof(T);

    public void Dispose()
    {
        if (!_disposed)
        {
            RSDebugger.Buffers.Remove(this);
            _buffer?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    public void BindAsVertexBuffer(uint slot = 0, uint offsetInBytes = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.VertexBuffer))
            throw new InvalidOperationException("Buffer was not created with VertexBuffer bind flag");
            
        D3D11DeviceContainer.SharedState.Context.IASetVertexBuffer(slot, _buffer, (uint)Marshal.SizeOf<T>(), offsetInBytes);
    }

    public void BindAsIndexBuffer(Format format = Format.R32_UInt, uint offsetInBytes = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.IndexBuffer))
            throw new InvalidOperationException("Buffer was not created with IndexBuffer bind flag");
        
        if (format != Format.R16_UInt && format != Format.R32_UInt)
            throw new ArgumentException("Index buffer format must be R16_UInt or R32_UInt", nameof(format));
            
        if (format == Format.R16_UInt && Marshal.SizeOf<T>() != 2)
            throw new InvalidOperationException("R16_UInt format requires a 16-bit index type");
            
        if (format == Format.R32_UInt && Marshal.SizeOf<T>() != 4)
            throw new InvalidOperationException("R32_UInt format requires a 32-bit index type");
            
        D3D11DeviceContainer.SharedState.Context.IASetIndexBuffer(_buffer, format, offsetInBytes);
    }

    public void BindAsConstantBuffer(ShaderStages shaderStage, uint slot)
    {
        ID3D11Buffer[] buffers = [_buffer];
        if (_disposed)
            throw new ObjectDisposedException(nameof(RSBuffer<T>));
            
        if (!BindFlags.HasFlag(BindFlags.ConstantBuffer))
            throw new InvalidOperationException("Buffer was not created with ConstantBuffer bind flag");
            
        if ((shaderStage & ShaderStages.Vertex) != 0)
            D3D11DeviceContainer.SharedState.Context.VSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Pixel) != 0)
            D3D11DeviceContainer.SharedState.Context.PSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Geometry) != 0)
            D3D11DeviceContainer.SharedState.Context.GSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Hull) != 0)
            D3D11DeviceContainer.SharedState.Context.HSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Domain) != 0)
            D3D11DeviceContainer.SharedState.Context.DSSetConstantBuffers(slot, buffers);
            
        if ((shaderStage & ShaderStages.Compute) != 0)
            D3D11DeviceContainer.SharedState.Context.CSSetConstantBuffers(slot, buffers);
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