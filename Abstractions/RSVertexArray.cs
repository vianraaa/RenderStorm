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
        private readonly ID3D11Device _device;
        
        public Action<ID3D11DeviceContext>? OnPreDraw;

        public RSVertexArray(ID3D11Device device, ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices,
            string debugName = "VertexArray")
        {
            _device = device;
            DebugName = debugName;

            _vertexBuffer = new RSBuffer<T>(device, vertices, BindFlags.VertexBuffer, debugName: debugName + "_vertexBuffer");
            _indexBuffer = new RSBuffer<uint>(device, indices, BindFlags.IndexBuffer, debugName: debugName + "_indexBuffer");
            
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

        public void Bind(D3D11DeviceContainer context)
        {
            _vertexBuffer?.BindAsVertexBuffer(context);
            _indexBuffer?.BindAsIndexBuffer(context);
        }
        public void DrawIndexed(D3D11DeviceContainer container)
        {
            var context = container.Context;
            Bind(container);
            OnPreDraw?.Invoke(context);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.DrawIndexed((uint)_indexBuffer.ItemCount, 0, 0);
        }
    }