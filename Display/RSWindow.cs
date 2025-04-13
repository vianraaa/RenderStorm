using System.Numerics;
using System.Text;
using ImGuiNET;
using RenderStorm.ImGuiImpl;
using RenderStorm.Other;
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
        public string CleanInfo { get; }

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
            
            Native = SDL.SDL_CreateWindow(title, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, width, height, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            if (Native == IntPtr.Zero)
            {
                SDL.SDL_Quit();
                throw new ApplicationException("Failed to create window: " + SDL.SDL_GetError());
            }
            
            SDL.SDL_SysWMinfo info = new SDL.SDL_SysWMinfo();
            SDL.SDL_GetWindowWMInfo(Native, ref info);
            D3dDeviceContainer = new D3D11DeviceContainer(info.info.win.window, (uint)width, (uint)height);
            CleanInfo = D3dDeviceContainer.GetGroupedInfo();
            IntPtr ctx = ImGui.CreateContext();
            ImGui.SetCurrentContext(ctx);
            ImGuiNative.igStyleSpectrum();
            ImGuiNative.igSetIODisplaySize(width, height);
            ImGuiNative.igSetIOFramebufferScale(1, 1);
            
            
            ImGuiSdl2Impl.ImGui_ImplSDL2_InitForD3D(ImGuiSdl2Impl.GetSDLWindow());
            ImGuiDx11Impl.ImGui_ImplDX11_Init(D3dDeviceContainer.Device.NativePointer, D3dDeviceContainer.Context.NativePointer);
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
            RSDebugger.Init(this);
            double lastFrameTime = SDL.SDL_GetTicks() / 1000.0;
            ViewBegin?.Invoke();
            D3dDeviceContainer.InitializeRenderStates();
            while (Running)
            {
                SDL.SDL_GetWindowSize(Native, out var width, out var height);
                SDL.SDL_Event e;
                while (SDL.SDL_PollEvent(out e) != 0)
                {
                    ImGuiSdlInput.Process(e);
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            Running = false;
                            break;
                        case SDL.SDL_EventType.SDL_KEYUP:
                            if(e.key.keysym.sym == SDL.SDL_Keycode.SDLK_F2)
                                DebuggerOpen = !DebuggerOpen;
                            break;
                        case SDL.SDL_EventType.SDL_WINDOWEVENT:
                            if (e.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
                            {
                                D3dDeviceContainer.Resize((uint)e.window.data1, (uint)e.window.data2);
                            }
                            break;
                    }
                }
                ImGuiDx11Impl.ImGui_ImplDX11_NewFrame();
                ImGuiSdl2Impl.ImGui_ImplSDL2_NewFrame();
                ImGuiNative.igSetIODisplaySize(width, height);
                ImGuiNative.igSetIOFramebufferScale(1, 1);
                ImGui.NewFrame();
                
                double currentTime = SDL.SDL_GetTicks() / 1000.0;
                double deltaTime = currentTime - lastFrameTime;
                lastFrameTime = currentTime;
                D3dDeviceContainer.ApplyRenderStates();
                D3dDeviceContainer.SetRenderTargets();
                D3dDeviceContainer.Clear(0.1f, 0.1f, 0.1f, 1.0f);
                ViewUpdate?.Invoke(deltaTime);
                RSDebugger.DeltaTime = deltaTime;
                RSDebugger.TimeElapsed += deltaTime;

                if (DebuggerOpen)
                {
                    RSDebugger.DrawDebugger(D3dDeviceContainer);
                }
                    
                if(DebugString)
                    RSDebugger.DrawDebugText($"{RSDebugger.RSVERSION}\n" +
                                             $"{CleanInfo} DirectX 11\n" +
                                             $"{(int)(1.0f / deltaTime)}fps");
                ImGui.Render();
                unsafe
                {
                    ImGuiDx11Impl.ImGui_ImplDX11_RenderDrawData(ImGui.GetDrawData());
                }
                D3dDeviceContainer.Present();
            }
            ViewEnd?.Invoke();
        }

        public void Dispose()
        {
            RSDebugger.Dispose();
            ImGuiSdl2Impl.ImGui_ImplSDL2_Shutdown();
            ImGuiDx11Impl.ImGui_ImplDX11_Shutdown();
            ImGui.DestroyContext();
            D3dDeviceContainer.Dispose();
            SDL.SDL_DestroyWindow(Native);
            SDL.SDL_Quit();
        }
    }
}