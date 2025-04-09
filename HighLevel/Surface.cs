using System;
using System.Numerics;
using RenderStorm.Abstractions;

namespace RenderStorm.HighLevel;

public class Surface<T>: ICommandQueueItem where T : unmanaged
{
    public string ViewProjMatrixUniform = "m_ViewProj";
    public string ModelMatrixUniform = "m_Model";
    
    public string DebugName { get; }
    public Type AllocatedType => typeof(T);
    public Matrix4x4 Model { get; set; }
    public Vector3 AABBMin { get; set; }
    public Vector3 AABBMax { get; set; }
    
    private readonly RSVertexArray<T> _array;
    private readonly RSShader _pipeline;

    public Surface(ReadOnlySpan<T> vertices, ReadOnlySpan<uint> indices, RSShader shader, string debugName)
    {
        DebugName = debugName;
        _pipeline = shader;
        Model = Matrix4x4.Identity;
        _array = new RSVertexArray<T>(vertices, indices);
    }
    public void Dispatch(Matrix4x4 matrix)
    {
        _pipeline.Use();
        _pipeline.SetUniform(ViewProjMatrixUniform, matrix);
        _pipeline.SetUniform(ModelMatrixUniform, Model);
        _array.DrawIndexed();
    }
    
    public void Dispose()
    {
        _array.Dispose();
        // _pipeline.Dispose(); // shader lifetime is managed by the engine
    }
}