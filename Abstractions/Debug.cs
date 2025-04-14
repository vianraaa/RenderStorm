using System;
using System.Runtime.CompilerServices;

namespace RenderStorm.Abstractions;

public class Debug
{
    internal static async void Crash(string message)
    {
        Error($"Crash: {message}");
        Environment.FailFast($"rs crash: {message}");
    }

    public static void Log(dynamic message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Console.WriteLine($"[ LOG {Path.GetFileName(file)} ( {caller} ) ] {message}");
    }

    public static void Error(dynamic message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ ERR {Path.GetFileName(file)} ( {caller} ) ] {message}");
        Console.ResetColor();
    }

    public static void Warn(dynamic message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[ WRN {Path.GetFileName(file)} ( {caller} ) ] {message}");
        Console.ResetColor();
    }
}