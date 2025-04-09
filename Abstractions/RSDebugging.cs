using System.Diagnostics;

namespace RenderStorm.Abstractions;

public enum LogType
{
    LOG_INFO,
    LOG_WARN,
    LOG_ERROR
}

public static class RSDebugging
{
    public static Action<string, LogType>? OnLog;

    public static void Log(string message, LogType logType = LogType.LOG_INFO)
    {
        OnLog?.Invoke(message, logType);
        var frame = new StackFrame(0);
        Console.Write("[");
        Console.Write(frame.GetMethod().DeclaringType.Name + ".");
        Console.Write(frame.GetMethod().Name);
        Console.Write("] ");
        switch (logType)
        {
            case LogType.LOG_INFO:
                Console.Write("INFO: ");
                break;
            case LogType.LOG_WARN:
                Console.Write("WARN: ");
                break;
            case LogType.LOG_ERROR:
                Console.Write("ERROR: ");
                break;
        }

        Console.WriteLine(message);
    }
}