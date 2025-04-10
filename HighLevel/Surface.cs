using System;
using System.Numerics;
using RenderStorm.Abstractions;

namespace RenderStorm.HighLevel;

public class Surface<T>: ICommandQueueItem where T : unmanaged
{
    public string DebugName { get; }
    public Type AllocatedType => typeof(T);
    public Matrix4x4 Model { get; set; }
    public Vector3 AABBMin { get; set; }
    public Vector3 AABBMax { get; set; }
    public Action<RSShader?> PreDraw { get; set; }
    public Action<RSShader?> PostDraw { get; set; }

    private RSVertexArray<T> _array;
    private bool _isExternalAllocation = false;

    public Surface(ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices, string debugName)
    {
        DebugName = debugName;
        Model = Matrix4x4.Identity;
        _array = new RSVertexArray<T>(vertices, indices);
    }
    public Surface(RSVertexArray<T> array, string debugName)
    {
        DebugName = debugName;
        Model = Matrix4x4.Identity;
        _array = array;
        _isExternalAllocation = true;
    }

    public void Dispatch(Matrix4x4 matrix, RSShader? shader)
    {
        shader?.SetUniform("m_Model", Model);
        _array.DrawIndexed();
    }

    public void Dispose()
    {
        if(!_isExternalAllocation)
            _array.Dispose();
        // _pipeline.Dispose(); // shader lifetime is managed by the engine
    }
}