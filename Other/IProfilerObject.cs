namespace RenderStorm.Other;

public class IProfilerObject
{
    public IProfilerObject()
    {
        NativeInstance = NativeInstCount;
        NativeInstCount++;
    }
    internal static int NativeInstCount = 0;
    internal int NativeInstance = 0;
    public string DebugName { get; protected set; } = "ProfilerObject";
}