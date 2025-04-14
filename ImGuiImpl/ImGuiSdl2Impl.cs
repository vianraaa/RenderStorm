using System.Runtime.InteropServices;
using ImGuiNET;
using SDL2;

namespace RenderStorm.ImGuiImpl;

public static class ImGuiSdl2Impl
{
    [DllImport("SDL2", EntryPoint = "SDL_GL_GetCurrentWindow", CallingConvention = (CallingConvention)2)]
    public static extern IntPtr GetSDLWindow();
    [DllImport("cimgui.dll")]
    public static extern bool ImGui_ImplSDL2_InitForD3D(IntPtr window);
    
    [DllImport("cimgui.dll")]
    public static extern void ImGui_ImplSDL2_Shutdown();
    
    [DllImport("cimgui.dll")]
    public static extern void ImGui_ImplSDL2_NewFrame();
}