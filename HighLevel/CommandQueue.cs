using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using RenderStorm.Abstractions;
using RenderStorm.Display;

namespace RenderStorm.HighLevel;

/// <summary>
/// Represents a frustum for efficient culling
/// </summary>
public struct Frustum
{
    // The 6 planes of the frustum (left, right, bottom, top, near, far)
    public Vector4[] Planes;

    public Frustum(Matrix4x4 viewProj)
    {
        Planes = new Vector4[6];

        // Left plane
        Planes[0] = new Vector4(
            viewProj.M14 + viewProj.M11,
            viewProj.M24 + viewProj.M21,
            viewProj.M34 + viewProj.M31,
            viewProj.M44 + viewProj.M41);

        // Right plane
        Planes[1] = new Vector4(
            viewProj.M14 - viewProj.M11,
            viewProj.M24 - viewProj.M21,
            viewProj.M34 - viewProj.M31,
            viewProj.M44 - viewProj.M41);

        // Bottom plane
        Planes[2] = new Vector4(
            viewProj.M14 + viewProj.M12,
            viewProj.M24 + viewProj.M22,
            viewProj.M34 + viewProj.M32,
            viewProj.M44 + viewProj.M42);

        // Top plane
        Planes[3] = new Vector4(
            viewProj.M14 - viewProj.M12,
            viewProj.M24 - viewProj.M22,
            viewProj.M34 - viewProj.M32,
            viewProj.M44 - viewProj.M42);

        // Near plane
        Planes[4] = new Vector4(
            viewProj.M13,
            viewProj.M23,
            viewProj.M33,
            viewProj.M43);

        // Far plane
        Planes[5] = new Vector4(
            viewProj.M14 - viewProj.M13,
            viewProj.M24 - viewProj.M23,
            viewProj.M34 - viewProj.M33,
            viewProj.M44 - viewProj.M43);

        // Normalize all planes
        for (int i = 0; i < 6; i++)
        {
            float length = (float)Math.Sqrt(Planes[i].X * Planes[i].X +
                                          Planes[i].Y * Planes[i].Y +
                                          Planes[i].Z * Planes[i].Z);
            Planes[i] /= length;
        }
    }

    /// <summary>
    /// Tests if an AABB is inside or intersecting the frustum
    /// </summary>
    public bool IsAABBVisible(Vector3 aabbMin, Vector3 aabbMax)
    {
        // Quick sphere test first as a fast rejection
        Vector3 center = (aabbMin + aabbMax) * 0.5f;
        float radius = Vector3.Distance(aabbMin, aabbMax) * 0.5f;

        for (int i = 0; i < 6; i++)
        {
            float distance = Planes[i].X * center.X +
                            Planes[i].Y * center.Y +
                            Planes[i].Z * center.Z +
                            Planes[i].W;

            if (distance < -radius)
                return false; // Outside the frustum
        }

        // More precise AABB test
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = aabbMin;
            if (Planes[i].X >= 0) p.X = aabbMax.X;
            if (Planes[i].Y >= 0) p.Y = aabbMax.Y;
            if (Planes[i].Z >= 0) p.Z = aabbMax.Z;

            float d = Planes[i].X * p.X +
                      Planes[i].Y * p.Y +
                      Planes[i].Z * p.Z +
                      Planes[i].W;

            if (d < 0) return false; // Outside the frustum
        }

        return true; // Inside or intersecting the frustum
    }
}

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

    public Action<RSShader?>? PreDraw { get; set; }
    public Action<RSShader?>? PostDraw { get; set; }
    public void Dispatch(Matrix4x4 matrix, RSShader? shader);
}

public class CommandQueue: IDisposable
{
    public readonly string DebugName = "Queue";
    public RSShader? DrawShader;
    public Queue<ICommandQueueItem> RenderQueue = new();
    public int DrawnItems = 0;
    internal List<long> CommandQueueTimes = new();
    internal long TotalTime = 0;

    private readonly Stopwatch totalStopwatch = new Stopwatch();
    private readonly Stopwatch commandStopwatch = new Stopwatch();


    public CommandQueue(string debugName, RSShader rsshader)
    {
        DrawShader = rsshader;
        DebugName = debugName;
    }
    /// <summary>
    /// Preprocesses and dispatches all attached command items.
    /// </summary>
    /// <param name="cam">The camera used for rendering data.</param>
    /// <param name="arguments">Arguments that control how preprocessing is done.</param>
    public void Dispatch(Camera cam, D3D11DeviceContainer container, DispatchArguments arguments = DispatchArguments.Default)
    {
        Matrix4x4 view = cam.GetView();
        Matrix4x4 proj = cam.GetProjection(RSWindow.Instance.GetAspect());
        if ((arguments & DispatchArguments.SwitchViewProjection) != 0)
        {
            Dispatch(view * proj, container, arguments);
        }
        else
        {
            Dispatch(view * proj, container, arguments);
        }

    }

    private Frustum _cachedFrustum;
    private Matrix4x4 _lastViewProjMatrix;
    private bool _frustumDirty = true;

    private bool IsInViewFrustum(ICommandQueueItem item, Matrix4x4 viewProj)
    {
        if (item.AABBMin == Vector3.Zero && item.AABBMax == Vector3.Zero)
            return true;

        // Only recalculate the frustum if the view-projection matrix has changed
        if (_frustumDirty || viewProj != _lastViewProjMatrix)
        {
            _cachedFrustum = new Frustum(viewProj);
            _lastViewProjMatrix = viewProj;
            _frustumDirty = false;
        }

        // Use the optimized frustum culling
        return _cachedFrustum.IsAABBVisible(item.AABBMin, item.AABBMax);
    }


    public void Dispatch(Matrix4x4 matrix, D3D11DeviceContainer container, DispatchArguments arguments = DispatchArguments.Default)
    {
        totalStopwatch.Restart();
        CommandQueueTimes.Clear();
        
        DrawShader?.Use(container);
        DrawShader?.SetUniform("m_ViewProj", matrix);
        var countCopy = RenderQueue.Count;
        for (int i = 0; i < countCopy; i++)
        {
            ICommandQueueItem command = RenderQueue.Dequeue();
            if (!IsInViewFrustum(command, matrix))
            {
                CommandQueueTimes.Add(-1);
                continue;
            }
            commandStopwatch.Restart();
            
            command.PreDraw?.Invoke(DrawShader);
            command.Dispatch(matrix, DrawShader);
            command.PostDraw?.Invoke(DrawShader);
            
            commandStopwatch.Stop();
            CommandQueueTimes.Add(commandStopwatch.ElapsedMilliseconds);
            DrawnItems++;
        }

        totalStopwatch.Stop();
        TotalTime = totalStopwatch.ElapsedMilliseconds;
    }

    public void Push(ICommandQueueItem item)
    {
        RenderQueue.Enqueue(item);
    }

    public void Dispose()
    {
        RSDebugger.Queues.Remove(this);
        RSDebugger.QueueNames.Remove(DebugName);
        foreach (var command in RenderQueue)
        {
            command.Dispose();
        }
    }
}