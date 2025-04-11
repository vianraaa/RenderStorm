using Vortice.Direct3D11;

namespace RenderStorm.Abstractions;

public interface IDrawableArray
{
    public void DrawIndexed(ID3D11DeviceContext context);
}