using Silk.NET.OpenGL;

namespace RenderStorm;

public struct OpenGL
{
    public static GL API;

    /// <summary>
    /// Toggles depth testing
    /// </summary>
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
    
    /// <summary>
    /// Toggles face culling
    /// </summary>
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