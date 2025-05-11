using RenderStorm;

namespace RenderStormImpl;

public static class Workarounds
{
    public static bool BeginNamedMenuBar(string title)
    {
        unsafe
        {
            byte[] titleBytes = System.Text.Encoding.UTF8.GetBytes(title + '\0');
            fixed (byte* titlePtr = titleBytes)
                return ImGuiNative.igBeginNamedMenuBar(titlePtr);
        }
    }

    public static void EndNamedMenuBar()
    {
        ImGui.EndMenuBar();
        ImGui.End();
    }
}