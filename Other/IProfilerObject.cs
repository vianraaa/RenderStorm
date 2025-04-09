namespace RenderStorm.Other;

public class IProfilerObject
{
    internal uint NativeInstance = 0;
    public string DebugName { get; protected set; } = "Shader";
}