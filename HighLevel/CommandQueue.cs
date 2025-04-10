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
    public int DrawnItems = 0;
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
        if (item.AABBMin == Vector3.Zero && item.AABBMax == Vector3.Zero)
            return true;
        
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(item.AABBMin.X, item.AABBMin.Y, item.AABBMin.Z);
        corners[1] = new Vector3(item.AABBMax.X, item.AABBMin.Y, item.AABBMin.Z);
        corners[2] = new Vector3(item.AABBMin.X, item.AABBMax.Y, item.AABBMin.Z);
        corners[3] = new Vector3(item.AABBMax.X, item.AABBMax.Y, item.AABBMin.Z);
        corners[4] = new Vector3(item.AABBMin.X, item.AABBMin.Y, item.AABBMax.Z);
        corners[5] = new Vector3(item.AABBMax.X, item.AABBMin.Y, item.AABBMax.Z);
        corners[6] = new Vector3(item.AABBMin.X, item.AABBMax.Y, item.AABBMax.Z);
        corners[7] = new Vector3(item.AABBMax.X, item.AABBMax.Y, item.AABBMax.Z);
        
        bool inside = false;
        foreach (var corner in corners)
        {
            Vector4 clipSpace = Vector4.Transform(new Vector4(corner, 1.0f), viewProj);
            
            if (clipSpace.W != 0)
            {
                Vector3 ndc = new Vector3(clipSpace.X, clipSpace.Y, clipSpace.Z) / clipSpace.W;
                
                if (ndc.X >= -1 && ndc.X <= 1 &&
                    ndc.Y >= -1 && ndc.Y <= 1 &&
                    ndc.Z >= -1 && ndc.Z <= 1)
                {
                    inside = true;
                    break;
                }
            }
        }

        return inside;
    }
    

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
            if(!IsInViewFrustum(command, Matrix4x4.Identity)) continue;
            commandStopwatch.Restart();
            command.Dispatch(matrix, DrawShader);
            commandStopwatch.Stop();
            CommandQueueTimes.Add(commandStopwatch.ElapsedMilliseconds);
            DrawnItems++;
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