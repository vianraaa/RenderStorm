using Silk.NET.OpenGL;

namespace RenderStorm;

public struct OpenGL
{
    public static GL API;

    public static bool DepthTest
    {
        set
        {
            if(value)
                API.Enable(EnableCap.DepthTest);
            else
                API.Disable(EnableCap.DepthTest);
        }
    }
    
    public static bool CullFace
    {
        set
        {
            if(value)
                API.Enable(EnableCap.CullFace);
            else
                API.Disable(EnableCap.CullFace);
        }
    }
}