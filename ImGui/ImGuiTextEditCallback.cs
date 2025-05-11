using System.Runtime.InteropServices;

namespace RenderStorm
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int ImGuiInputTextCallback(ImGuiInputTextCallbackData* data);
}
