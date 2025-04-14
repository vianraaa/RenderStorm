using System.Diagnostics;
using System.Diagnostics.Contracts;
namespace RenderStorm.Other;

public static class Util
{
    [Pure]
    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }
}