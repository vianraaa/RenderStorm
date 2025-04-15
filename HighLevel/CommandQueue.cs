using System.Numerics;
using RenderStorm.Abstractions;
using RenderStorm.Display;

namespace RenderStorm.HighLevel;

public interface ICommandQueueItem
{
    public bool VisibilityChecks { get; set; }
    public Vector3? AABBMin { get; protected set; }
    public Vector3? AABBMax { get; protected set; }
    public Matrix4x4 Transform { get; protected set; }
    public CommandQueue ParentQueue { get; internal set; }
    public Action<D3D11DeviceContainer, RSShader>? DispatchCallback  { get; set; }
    public void Dispatch(D3D11DeviceContainer container, RSShader shader);
}
public struct CommandQueueData
{
    public Matrix4x4 ViewProjectionMatrix;
    public Matrix4x4 ModelMatrix;
}
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
public class CommandQueue(RSShader shader)
{
    private Queue<ICommandQueueItem> queue = new();
    public RSShader Shader { get; } = shader;
    public CommandQueueData QueueData = new();
    
    private Frustum _cachedFrustum;
    private Matrix4x4 _lastViewProjMatrix;
    private bool _frustumDirty = true;

    private bool IsInViewFrustum(ICommandQueueItem item, Matrix4x4 viewProj)
    {
        if (item.AABBMin == Vector3.Zero && item.AABBMax == Vector3.Zero)
            return true;
        if (item.AABBMin == null || item.AABBMax == null)
            return true;
        
        if (_frustumDirty || viewProj != _lastViewProjMatrix)
        {
            _cachedFrustum = new Frustum(viewProj);
            _lastViewProjMatrix = viewProj;
            _frustumDirty = false;
        }
        return _cachedFrustum.IsAABBVisible(item.AABBMin.Value, item.AABBMax.Value);
    }

    public void DispatchQueue(D3D11DeviceContainer container)
    {
        Shader.Use(container); // if not used already
        Shader.SetCBuffer(container, 0, QueueData);
        for (int i = 0; i < queue.Count; i++)
        {
            ICommandQueueItem queueItem = queue.Dequeue();
            if(queueItem.VisibilityChecks && 
               !IsInViewFrustum(queueItem, QueueData.ViewProjectionMatrix))
            {
                queue.Enqueue(queueItem);
                continue;
            }

            queueItem.ParentQueue = this;
            queueItem.DispatchCallback?.Invoke(container, Shader);
            QueueData.ModelMatrix = queueItem.Transform;
            Shader.SetCBuffer(container, 0, QueueData);
            queueItem.Dispatch(container, Shader);
            queue.Enqueue(queueItem);
        }
    }

    public void Clear()
    {
        queue.Clear();
    }

    public void Push(ICommandQueueItem array)
    {
        queue.Enqueue(array);
    }
}