using System.Numerics;
using RenderStorm.Display;

namespace RenderStorm.HighLevel;

/// <summary>
/// Allows you to control some parts of the Dispatch command in rendering
/// </summary>
[Flags] public enum DispatchArguments
{
    /// <summary>
    /// The default dispatch argument.
    /// </summary>
    Default = 1,
    /// <summary>
    /// Generally used in XR, unless switched in shader code. 
    /// In that case, avoid using this value.
    /// </summary>
    SwitchViewProjection
}

/// <summary>
/// Represents an item in a command queue that can be dispatched for rendering.
/// </summary>
public interface ICommandQueueItem: IDisposable
{
    /// <summary>
    /// Used for culling and other operations
    /// </summary>
    Vector3 AABBMin { get; set; }
    /// <summary>
    /// Used for culling and other operations
    /// </summary>
    Vector3 AABBMax { get; set; }
    public void Dispatch(Matrix4x4 view, Matrix4x4 projection);
}

public struct DrawContext
{
    public bool DepthTesting = true;
    public bool CullFace = true;

    public DrawContext()
    {
    }
}

public class CommandQueue: IDisposable
{
    public DrawContext DrawContext = new DrawContext();
    private List<ICommandQueueItem> _commandQueue = new List<ICommandQueueItem>();
    /// <summary>
    /// Preprocesses and dispatches all attached command items.
    /// </summary>
    /// <param name="cam">The camera used for rendering data.</param>
    /// <param name="arguments">Arguments that control how preprocessing is done.</param>
    public void Dispatch(Camera cam, DispatchArguments arguments = DispatchArguments.Default)
    {
        Matrix4x4 view = cam.GetView();
        Matrix4x4 proj = cam.GetProjection(RSWindow.Instance.GetAspect());
        Dispatch(view, proj, arguments);
    }

    public void Dispatch(Matrix4x4 view, Matrix4x4 projection, DispatchArguments arguments = DispatchArguments.Default)
    {
        Matrix4x4 realView = view;
        Matrix4x4 realProjection = projection;
        
        if (arguments.HasFlag(DispatchArguments.SwitchViewProjection))
        {
            realView = realProjection;
            realProjection = projection;
        }
        // apply our draw context before dispatching
        OpenGL.DepthTest = DrawContext.DepthTesting;
        OpenGL.CullFace = DrawContext.CullFace;
        foreach (var command in _commandQueue)
        {
            command.Dispatch(realView, realProjection);
        }
    }

    public void Add(ICommandQueueItem item)
    {
        _commandQueue.Add(item);
    }
    
    public void Remove(ICommandQueueItem item)
    {
        _commandQueue.Remove(item);
    }
    
    public void Dispose()
    {
        foreach (var command in _commandQueue)
        {
            command.Dispose();
        }
    }
}