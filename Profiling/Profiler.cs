using ImGuiNET;

namespace RenderStorm.Profiling;

public class ProfilerStack
{
    public string Name;
    public long Time;
    public List<ProfilerStack> Children = new();
}

public static class Profiler
{
    private static Stack<KeyValuePair<ProfilerStack, long>> _executionStack = new();
    private static Stack<ProfilerStack> _nodeStack = new();
    private static ProfilerStack _executionContext = new();
    private static ProfilerStack _executionContextPrev = new();
    

    internal static void FrameBegin()
    {
        if(_executionStack.Count!=0)
            throw new Exception("Profiler stack is not empty! PopExecution has not been called.");
        _executionContextPrev = _executionContext;
        _executionContext = new ProfilerStack { Name = "Frame" };
        _executionStack.Clear();
        _nodeStack.Clear();
        _nodeStack.Push(_executionContext);
    }
    public static ProfilerStack GetExecutionContext()
    {
        return _executionContextPrev;
    }
    public static void PushExecution(string name)
    {
        var node = new ProfilerStack { Name = name };
        _nodeStack.Peek().Children.Add(node);
        _nodeStack.Push(node);
        _executionStack.Push(new KeyValuePair<ProfilerStack, long>(node, Environment.TickCount64));
    }
    public static long PopExecution()
    {
        var kvp = _executionStack.Pop();
        long startTick = kvp.Value;
        long duration = Environment.TickCount64 - startTick;
        _nodeStack.Pop().Time = duration;
        return duration;
    }

    public static void DrawStack(ProfilerStack? stack = null)
    {
        //todo
    }
}