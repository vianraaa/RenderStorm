using System.Numerics;
using RenderStorm.Abstractions;
using RenderStorm.Display;

namespace RenderStorm.HighLevel;

public class Surface<T>: ICommandQueueItem, IDisposable 
    where T : unmanaged
{
    public bool VisibilityChecks { get; set; } = true;
    public Vector3? AABBMin { get; set; }
    public Vector3? AABBMax { get; set; }
    public Matrix4x4 Transform { get; set; }
    public CommandQueue ParentQueue { get; set; }
    public Action<D3D11DeviceContainer, RSShader>? DispatchCallback { get; set; }
    private bool _dispose;
    private RSVertexArray<T> _array;

    public Surface(RSVertexArray<T> array, bool dispose = true)
    {
        _dispose = dispose;
        _array = array;
    }
    public void Dispatch(D3D11DeviceContainer container, RSShader shader)
    {
        _array.DrawIndexed(container);
    }

    public void Dispose()
    {
        if(_dispose)
            _array.Dispose();
    }
}