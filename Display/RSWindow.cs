using System;
using System.Numerics;
using GLFW;
using RenderStorm.Abstractions;
using RenderStorm.Other;
using RenderStorm.RSImGui;
using Silk.NET.OpenGL;
using Monitor = GLFW.Monitor;

namespace RenderStorm.Display;

public class RSWindow: IDisposable
{
    public static RSWindow Instance;
    private string _title = "Game";
    public GL API;
    public readonly Window Native;
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
    
    public string CachePath = Path.GetFullPath(".renderstorm");

    public RSWindow(string title = "Game", int width = 1024, int height = 600)
    {
        Instance = this;
        Glfw.Init();
        Glfw.WindowHint(Hint.ContextVersionMajor, 3);
        Glfw.WindowHint(Hint.ContextVersionMinor, 3);
        Glfw.WindowHint(Hint.OpenglProfile, Profile.Core);

        Native = Glfw.CreateWindow(width, height, title, Monitor.None, Window.None);
        if (Native == Window.None)
        {
            Glfw.Terminate();
            throw new ApplicationException("Failed to create window");
        }

        Glfw.MakeContextCurrent(Native);

        API = GL.GetApi(GetProcAddress);
        OpenGL.API = API;
        Title = title;
        ImGuiController = new ImGuiController(Native, width, height, OpenGL.API);
    }

    public Vector2 GetSize()
    {
        Glfw.GetWindowSize(Native, out var width, out var height);
        return new Vector2(width, height);
    }
    
    public float GetAspect()
    {
        Glfw.GetWindowSize(Native, out var width, out var height);
        return width / (float)height;
    }

    public ImGuiController ImGuiController { get; }

    public string Title
    {
        get => _title;
        set
        {
            Glfw.SetWindowTitle(Native, value);
            _title = value;
        }
    }

    private nint GetProcAddress(string name)
    {
        return Glfw.GetProcAddress(name);
    }

    public void Run()
    {
        Glfw.SwapInterval(0);
        API.ClearColor(0f, 0f, 0f, 1.0f);
        API.Enable(EnableCap.FramebufferSrgb);
        RSDebugger.Init(this);
        KeyCallback? old = null;
        old = Glfw.SetKeyCallback(Native, (window, key, scancode, action, mods) =>
        {
            old?.Invoke(window, key, scancode, action, mods);
            if (key == Keys.F2 && action == InputState.Press)
            {
                DebuggerOpen = !DebuggerOpen;
            }
        });

        ViewBegin?.Invoke();
        double dt = 1.0f / 60.0f;
        while (Running)
        {
            if (Glfw.WindowShouldClose(Native))
                Running = false;
            Glfw.Time = 0;
            Glfw.PollEvents();
            Glfw.GetFramebufferSize(Native, out var width, out var height);
            OpenGL.API.Viewport(0, 0, (uint)width, (uint)height);
            OpenGL.API.Clear(ClearBufferMask.ColorBufferBit |
                             ClearBufferMask.DepthBufferBit |
                             ClearBufferMask.StencilBufferBit);
            ImGuiController.WindowResized(width, height);
            ImGuiController.Update((float)dt);
            ViewUpdate?.Invoke(dt);
            if (DebuggerOpen)
                RSDebugger.DrawDebugger();
            if(DebugString)
                RSDebugger.DrawDebugText($"{RSDebugger.RSVERSION}\n{(int)(1.0f / dt)}fps");
            ImGuiController.Render();
            Glfw.SwapBuffers(Native);
            dt = Glfw.Time;
            RSDebugger.DeltaTime = dt;
            RSDebugger.TimeElapsed += dt;
        }
    }

    public void Dispose()
    {
        ViewEnd?.Invoke();
        RSDebugger.Dispose();
        ImGuiController.Dispose();
        RSShader.Shutdown();
        Glfw.DestroyWindow(Native);
        Glfw.Terminate();
    }
}