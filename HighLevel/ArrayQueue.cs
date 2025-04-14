using RenderStorm.Abstractions;
using RenderStorm.Display;

namespace RenderStorm.HighLevel;

public class ArrayQueue<T>(RSShader<T> shader)
    where T : unmanaged
{
    private Queue<RSVertexArray<T>> queue = new();
    public RSShader<T> Shader { get; } = shader;

    public void DispatchQueue(D3D11DeviceContainer container)
    {
        Shader.Use(container); // if not used already
        for (int i = 0; i < queue.Count; i++)
        {
            RSVertexArray<T> queueItem = queue.Dequeue();
            queueItem.DrawIndexed(container);
        }
    }

    public void Push(RSVertexArray<T> array)
    {
        queue.Enqueue(array);
    }
}