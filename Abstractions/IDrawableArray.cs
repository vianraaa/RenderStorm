namespace RenderStorm.Abstractions;

public interface IDrawableArray
{
    public string VertexBufferName { get; }
    public string IndexBufferName { get; }
    public int VertexBufferIndex { get; }
    public int IndexBufferIndex { get; }
    public void DrawIndexed();
}