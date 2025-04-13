using System;
using System.Reflection;
using System.Runtime.InteropServices;
using RenderStorm.Display;
using RenderStorm.Types;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RenderStorm.Abstractions;

public class RSVertexArray<T> : IDrawableArray, IDisposable where T : unmanaged
    {
        private bool _disposed;
        private readonly RSBuffer<uint>? _indexBuffer;
        private readonly RSBuffer<T>? _vertexBuffer;
        private ID3D11InputLayout? _inputLayout;
        private readonly ID3D11Device _device;
        private RSShader<T> _shader;
        public string DebugName { get; }

        public RSVertexArray(ID3D11Device device, ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices, RSShader<T> shader,
            string debugName = "VertexArray")
        {
            _shader = shader;
            _device = device;
            DebugName = debugName;

            _vertexBuffer = new RSBuffer<T>(device, vertices, BindFlags.VertexBuffer, debugName: debugName);
            _indexBuffer = new RSBuffer<uint>(device, indices, BindFlags.IndexBuffer, debugName: debugName);

            _inputLayout = _shader.InputLayout;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _indexBuffer?.Dispose();
                _vertexBuffer?.Dispose();
                _inputLayout?.Dispose();
                _disposed = true;
            }
        }

        public void Bind(D3D11DeviceContainer context)
        {
            _shader.Use(context);
            _vertexBuffer?.BindAsVertexBuffer(context);
            _indexBuffer?.BindAsIndexBuffer(context);
        }

        public void DrawIndexed(D3D11DeviceContainer container)
        {
            var context = container.Context;
            Bind(container);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.DrawIndexed((uint)_indexBuffer.ItemCount, 0, 0);
        }
    }