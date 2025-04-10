using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStorm.Other;

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
    public string DebugName { get; }
    public Type AllocatedType { get; }
    /// <summary>
    /// Used for culling and other operations
    /// </summary>
    public Vector3 AABBMin { get; set; }
    /// <summary>
    /// Used for culling and other operations
    /// </summary>
    public Vector3 AABBMax { get; set; }
    public void Dispatch(Matrix4x4 matrix, RSShader? shader);
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
    public readonly string DebugName = "Queue";
    public DrawContext DrawContext = new();
    public RSShader? DrawShader;
    public List<ICommandQueueItem> CommandQueueList = new();
    internal List<long> CommandQueueTimes = new();
    internal long TotalTime = 0;

    private readonly Stopwatch totalStopwatch = new Stopwatch();
    private readonly Stopwatch commandStopwatch = new Stopwatch();


    public CommandQueue(string debugName, RSShader rsshader)
    {
        DrawShader = rsshader;
        DebugName = debugName;
        RSDebugger.Queues.Add(this);
        RSDebugger.QueueNames.Add(DebugName);
    }
    /// <summary>
    /// Preprocesses and dispatches all attached command items.
    /// </summary>
    /// <param name="cam">The camera used for rendering data.</param>
    /// <param name="arguments">Arguments that control how preprocessing is done.</param>
    public void Dispatch(Camera cam, DispatchArguments arguments = DispatchArguments.Default)
    {
        Matrix4x4 view = cam.GetView();
        Matrix4x4 proj = cam.GetProjection(RSWindow.Instance.GetAspect());
        if ((arguments & DispatchArguments.SwitchViewProjection) != 0)
        {
            Dispatch(view * proj, arguments);
        }
        else
        {
            Dispatch(view * proj, arguments);
        }

    }
    
    private bool IsInViewFrustum(ICommandQueueItem item, Matrix4x4 viewProj)
    {
        // if no bounding box is set, always render
        if (item.AABBMin == Vector3.Zero && item.AABBMax == Vector3.Zero)
            return true;

        Matrix4x4 model = Matrix4x4.Identity;

        // Try to get the model matrix if the item is a Surface
        if (item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(Surface<>))
        {
            var modelProperty = item.GetType().GetProperty("Model");
            if (modelProperty != null)
            {
                model = (Matrix4x4)modelProperty.GetValue(item);
            }
        }

        var transformedMin = Vector3.Transform(item.AABBMin, model);
        var transformedMax = Vector3.Transform(item.AABBMax, model);

        // AABB corners
        var corners = new[]
        {
            new Vector4(transformedMin.X, transformedMin.Y, transformedMin.Z, 1),
            new Vector4(transformedMax.X, transformedMin.Y, transformedMin.Z, 1),
            new Vector4(transformedMin.X, transformedMax.Y, transformedMin.Z, 1),
            new Vector4(transformedMax.X, transformedMax.Y, transformedMin.Z, 1),
            new Vector4(transformedMin.X, transformedMin.Y, transformedMax.Z, 1),
            new Vector4(transformedMax.X, transformedMin.Y, transformedMax.Z, 1),
            new Vector4(transformedMin.X, transformedMax.Y, transformedMax.Z, 1),
            new Vector4(transformedMax.X, transformedMax.Y, transformedMax.Z, 1)
        };

        // transform corners to clip space
        var transformedCorners = new Vector4[8];
        for (int i = 0; i < 8; i++)
        {
            transformedCorners[i] = Vector4.Transform(corners[i], viewProj);
        }

        // check if corners are outside of frustum plane
        // left plane
        if (transformedCorners.All(c => c.X < -c.W)) return false;

        // right plane
        if (transformedCorners.All(c => c.X > c.W)) return false;

        // bottom plane
        if (transformedCorners.All(c => c.Y < -c.W)) return false;

        // top plane
        if (transformedCorners.All(c => c.Y > c.W)) return false;

        // near plane
        if (transformedCorners.All(c => c.Z < -c.W)) return false;

        // far plane
        if (transformedCorners.All(c => c.Z > c.W)) return false;

        // at this point, AAB is inside the frustum
        return true;
    }

    // batch rendering
    private List<ICommandQueueItem> _visibleItems = new List<ICommandQueueItem>();

    public void Dispatch(Matrix4x4 matrix, DispatchArguments arguments = DispatchArguments.Default)
    {
        totalStopwatch.Restart();
        CommandQueueTimes.Clear();

        // apply renders state only once 
        OpenGL.DepthTest = DrawContext.DepthTesting;
        OpenGL.CullFace = DrawContext.CullFace;
        DrawShader?.Use();
        DrawShader?.SetUniform("m_ViewProj", matrix);
        foreach (var command in CommandQueueList)
        {
            commandStopwatch.Restart();
            command.Dispatch(matrix, DrawShader);
            commandStopwatch.Stop();
            CommandQueueTimes.Add(commandStopwatch.ElapsedMilliseconds);
        }

        totalStopwatch.Stop();
        TotalTime = totalStopwatch.ElapsedMilliseconds;
    }

    public void Add(ICommandQueueItem item)
    {
        CommandQueueList.Add(item);
    }

    public void Remove(ICommandQueueItem item)
    {
        CommandQueueList.Remove(item);
    }

    public void Dispose()
    {
        RSDebugger.Queues.Remove(this);
        RSDebugger.QueueNames.Remove(DebugName);
        foreach (var command in CommandQueueList)
        {
            command.Dispose();
        }
    }
}