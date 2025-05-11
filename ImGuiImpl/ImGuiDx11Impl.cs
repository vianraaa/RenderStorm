using System.Runtime.InteropServices;
using RenderStorm;
using Vortice.Direct3D11;

namespace RenderStormImpl;

public static unsafe class ImGuiDx11Impl
{
    [DllImport("cimgui.dll")] 
    public static extern bool ImGui_ImplDX11_Init(IntPtr device, IntPtr device_context);
    [DllImport("cimgui.dll")]
    public static extern void ImGui_ImplDX11_Shutdown();
    [DllImport("cimgui.dll")]
    public static extern void ImGui_ImplDX11_NewFrame();
    [DllImport("cimgui.dll")]
    public static extern void ImGui_ImplDX11_RenderDrawData( ImDrawData* draw_data );
}