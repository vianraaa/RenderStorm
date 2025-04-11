using System.Numerics;
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
            double dt = 1.0f / 60.0f;
            ViewBegin?.Invoke();
            while (Running)
            {
                // Poll events
                SDL.SDL_Event e;
                while (SDL.SDL_PollEvent(out e) != 0)
                {
                    // Handle events
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_QUIT:
                            Running = false;
                            break;
                    }
                }
                
                double lastFrameTime = SDL.SDL_GetTicks() / 1000.0;
                double deltaTime = lastFrameTime - dt;
                dt = lastFrameTime;
                
                D3dDeviceContainer.Clear(0.1f, 0.1f, 0.1f, 1.0f); // Clear to black
                D3dDeviceContainer.SetRenderTargets();
                D3dDeviceContainer.ApplyDefaultStates();
                
                ViewUpdate?.Invoke(deltaTime);
                
                D3dDeviceContainer.Present();
                
                SDL.SDL_GL_SwapWindow(Native);
            }
            ViewEnd?.Invoke();
        }

        public void Dispose()
        {
            D3dDeviceContainer.Dispose();
            SDL.SDL_DestroyWindow(Native);
            SDL.SDL_Quit();
        }
    }
}
