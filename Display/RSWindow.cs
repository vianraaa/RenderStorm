using System.Numerics;
using ImGuiNET;
using RenderStorm.ImGuiImpl;
using SDL2;

namespace RenderStorm.Display
{
    public class RSWindow : IDisposable
    {
        public static RSWindow Instance;
        private string _title = "Game";
        public IntPtr Native;
        public Action ViewBegin;
        public Action ViewEnd;
        public Action<double> ViewUpdate;
        public bool Running = true;

        public bool DebuggerOpen = false;
#if DEBUG
        public bool DebugString = true;
#else
        public bool DebugString = false;
#endif

        private string _cachePath;
        public readonly D3D11DeviceContainer D3dDeviceContainer;

        public string CachePath
        {
            get => _cachePath;
            set
            {
                _cachePath = value;
                Directory.CreateDirectory(Path.GetFullPath(_cachePath));
            }
        }

        public RSWindow(string title = "Game", int width = 1024, int height = 600)
        {
            CachePath = Path.GetFullPath(".renderstorm");
            Instance = this;
            
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) != 0)
            {
                throw new ApplicationException("Failed to initialize SDL: " + SDL.SDL_GetError());
            }

            // Create an SDL window
            Native = SDL.SDL_CreateWindow(title, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, width, height, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (Native == IntPtr.Zero)
            {
                SDL.SDL_Quit();
                throw new ApplicationException("Failed to create window: " + SDL.SDL_GetError());
            }

            // Create the Direct3D 11 device container
            SDL.SDL_SysWMinfo info = new SDL.SDL_SysWMinfo();
            SDL.SDL_GetWindowWMInfo(Native, ref info);
            D3dDeviceContainer = new D3D11DeviceContainer(info.info.win.window, (uint)width, (uint)height);
            IntPtr ctx = ImGui.CreateContext();
            ImGui.SetCurrentContext(ctx);
            ImGui.StyleColorsDark();
            
            var io = ImGui.GetIO();
            io.Fonts.AddFontDefault();
            
            ImGuiSdl2Impl.ImGui_ImplSDL2_InitForD3D(ImGuiSdl2Impl.GetSDLWindow());
            ImGuiDx11Impl.ImGui_ImplDX11_Init(D3dDeviceContainer.Device.NativePointer, D3dDeviceContainer.Context.NativePointer);
            io = ImGui.GetIO();
            io.DisplaySize.X = width;
            io.DisplaySize.Y = height;
        }

        public Vector2 GetSize()
        {
            SDL.SDL_GetWindowSize(Native, out var width, out var height);
            return new Vector2(width, height);
        }

        public float GetAspect()
        {
            SDL.SDL_GetWindowSize(Native, out var width, out var height);
            return width / (float)height;
        }

        public string Title
        {
            get => _title;
            set
            {
                SDL.SDL_SetWindowTitle(Native, value);
                _title = value;
            }
        }

        public void Run()
        {
            try
            {
                double lastFrameTime = SDL.SDL_GetTicks() / 1000.0;
                ViewBegin?.Invoke();
                D3dDeviceContainer.InitializeRenderStates();
                while (Running)
                {
                    SDL.SDL_Event e;
                    while (SDL.SDL_PollEvent(out e) != 0)
                    {
                        unsafe
                        {
                            ImGuiSdl2Impl.ImGui_ImplSDL2_ProcessEvent(&e);
                        }
                        switch (e.type)
                        {
                            case SDL.SDL_EventType.SDL_QUIT:
                                Running = false;
                                break;
                        }
                    }
                
                    ImGuiDx11Impl.ImGui_ImplDX11_NewFrame();
                    ImGuiSdl2Impl.ImGui_ImplSDL2_NewFrame();
                    ImGui.NewFrame();
                
                    double currentTime = SDL.SDL_GetTicks() / 1000.0;
                    double deltaTime = currentTime - lastFrameTime;
                    lastFrameTime = currentTime;
                    D3dDeviceContainer.ApplyRenderStates();
                    D3dDeviceContainer.SetRenderTargets();
                    D3dDeviceContainer.Clear(0.1f, 0.1f, 0.1f, 1.0f);
                    ViewUpdate?.Invoke(deltaTime);
                
                    ImGui.ShowDemoWindow();
                
                    ImGui.Render();
                    unsafe
                    {
                        ImGuiDx11Impl.ImGui_ImplDX11_RenderDrawData(ImGui.GetDrawData().NativePtr);
                    }
                    D3dDeviceContainer.Present();
                }
    
                ViewEnd?.Invoke();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void Dispose()
        {
            ImGuiSdl2Impl.ImGui_ImplSDL2_Shutdown();
            ImGuiDx11Impl.ImGui_ImplDX11_Shutdown();
            ImGui.DestroyContext();
            D3dDeviceContainer.Dispose();
            SDL.SDL_DestroyWindow(Native);
            SDL.SDL_Quit();
        }
    }
}
