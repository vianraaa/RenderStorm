using System.Runtime.InteropServices;
using RenderStorm;
using Vortice.Direct3D11;

namespace RenderStormImpl;

public static unsafe class ImGuiDx11Impl
{
    [DllImport("cimgui.dll", EntryPoint = "ImGui_ImplDX11_Init")] 
    public static extern bool Init(IntPtr device, IntPtr device_context);
    [DllImport("cimgui.dll", EntryPoint = "ImGui_ImplDX11_Shutdown")] 
    public static extern void Shutdown();
    [DllImport("cimgui.dll", EntryPoint = "ImGui_ImplDX11_NewFrame")] 
    public static extern void NewFrame();
    [DllImport("cimgui.dll", EntryPoint = "ImGui_ImplDX11_RenderDrawData")] 
    public static extern void RenderDrawData( ImDrawData* draw_data );
    [DllImport("cimgui.dll", EntryPoint = "ImGui_ImplDX11_CreateDeviceObjects")] 
    public static extern void CreateDeviceObjects();
}