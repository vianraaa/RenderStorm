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
    [Pure]
    public static float Deg2Rad(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    [Pure]
    public static float Rad2Deg(float radians)
    {
        return radians * (180f / MathF.PI);
    }
}