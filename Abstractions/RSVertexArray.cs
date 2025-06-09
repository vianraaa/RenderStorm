using System;
using System.Reflection;
using System.Runtime.InteropServices;
using RenderStorm.Display;
using RenderStorm.Other;
using RenderStorm.Types;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RenderStorm.Abstractions;

public class RSVertexArray<T> :  IProfilerObject, IDrawableArray, IDisposable where T : unmanaged
    {
        private bool _disposed;
        private readonly RSBuffer<uint>? _indexBuffer;
        private readonly RSBuffer<T>? _vertexBuffer;

        public RSVertexArray(ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices,
            string debugName = "VertexArray")
        {
            DebugName = debugName;

            _vertexBuffer = new RSBuffer<T>(vertices, BindFlags.VertexBuffer, debugName: debugName + "_vertexBuffer");
            _indexBuffer = new RSBuffer<uint>(indices, BindFlags.IndexBuffer, debugName: debugName + "_indexBuffer");
            
            RSDebugger.VertexArrays.Add(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                RSDebugger.VertexArrays.Remove(this);
                _indexBuffer?.Dispose();
                _vertexBuffer?.Dispose();
                _disposed = true;
            }
        }

        public void Bind()
        {
            _vertexBuffer?.BindAsVertexBuffer();
            _indexBuffer?.BindAsIndexBuffer();
        }
        public void DrawIndexed()
        {
            Bind();
            D3D11DeviceContainer.SharedState.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            D3D11DeviceContainer.SharedState.Context.DrawIndexed((uint)_indexBuffer.ItemCount, 0, 0);
        }
    }