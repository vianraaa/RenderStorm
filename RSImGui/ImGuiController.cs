using System.Drawing;
using System.Numerics;
using GLFW;
using ImGuiNET;
using RenderStorm.Other;
using Silk.NET.OpenGL;

namespace RenderStorm.RSImGui;

public class ImGuiController : IDisposable
{
    public static bool CanScroll = true;
    public static Action<float>? OnScroll;
    
    private readonly List<char> _pressedChars = new();
    private int _attribLocationProjMtx;

    private int _attribLocationTex;
    private int _attribLocationVtxColor;
    private int _attribLocationVtxPos;
    private int _attribLocationVtxUV;
    private uint _elementsHandle;

    private ImGuiTexture _fontTexture;
    private bool _frameBegun;
    private GL _gl;
    private ImGuiShader _shader;
    private uint _vboHandle;
    private uint _vertexArrayObject;
    private int _windowHeight;

    private int _windowWidth;

    public IntPtr Context;

    public Window GlfwWindow;

    private float mouse_h, mouse;

    /// <summary>
    ///     Constructs a new ImGuiController with font configuration and onConfigure Action.
    /// </summary>
    public ImGuiController(Window win, int width, int height, GL gl, Action onConfigureIO = null)
    {
        GlfwWindow = win;
        Init(gl, width, height);

        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();

        onConfigureIO?.Invoke();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        SetupTheme();

        CreateDeviceResources();

        SetPerFrameImGuiData(1f / 60f);

        BeginFrame();
    }

    /// <summary>
    ///     Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        _gl.DeleteBuffer(_vboHandle);
        _gl.DeleteBuffer(_elementsHandle);
        _gl.DeleteVertexArray(_vertexArrayObject);

        _fontTexture.Dispose();
        _shader.Dispose();

        ImGui.DestroyContext(Context);
    }

    public void SetupTheme()
    {
    }

    private void Init(GL gl, int width, int height)
    {
        KeyCallback? old = null;
        old = Glfw.SetKeyCallback(GlfwWindow, (window, key, scancode, action, mods) =>
        {
            if (action == InputState.Press)
            {
                OnKeyDown(scancode, key);
            }
            else if (action == InputState.Release)
            {
                OnKeyUp(scancode, key);
            }
            else if (action == InputState.Repeat)
            {
            }
            old?.Invoke(window, key, scancode, action, mods);
        });
        Glfw.SetScrollCallback(GlfwWindow, (window, xOffset, yOffset) =>
        {
            OnScroll?.Invoke((float)yOffset);
            var io = ImGui.GetIO();
            if (CanScroll)
            {
                io.MouseWheel += (float)yOffset;
                io.MouseWheelH += (float)xOffset;
            }
        });
        _gl = gl;
        _windowWidth = width;
        _windowHeight = height;

        Context = ImGui.CreateContext();
        ImGui.SetCurrentContext(Context);
        ImGui.StyleColorsDark();
    }

    public void BeginFrame()
    {
        ImGui.NewFrame();
        _frameBegun = true;
    }

    private static void OnKeyDown(int scancode, Keys key)
    {
        OnKeyEvent(key, scancode, true);
    }

    private static void OnKeyUp(int scancode, Keys key)
    {
        OnKeyEvent(key, scancode, false);
    }

    /// <summary>
    ///     Delegate to receive keyboard key events.
    /// </summary>
    /// <param name="keyboard">The keyboard context generating the event.</param>
    /// <param name="keycode">The native keycode of the key generating the event.</param>
    /// <param name="scancode">The native scancode of the key generating the event.</param>
    /// <param name="down">True if the event is a key down event, otherwise False</param>
    private static void OnKeyEvent(Keys keycode, int scancode, bool down)
    {
        var io = ImGui.GetIO();
        var imGuiKey = TranslateInputKeyToImGuiKey(keycode);
        io.AddKeyEvent(imGuiKey, down);
        io.SetKeyEventNativeData(imGuiKey, (int)keycode, scancode);
    }

    private void OnKeyChar(char arg2)
    {
        _pressedChars.Add(arg2);
    }

    public void WindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    /// <summary>
    ///     Renders the ImGui draw list data.
    ///     This method requires a <see cref="GraphicsDevice" /> because it may create new DeviceBuffers if the size of vertex
    ///     or index data has increased beyond the capacity of the existing buffers.
    ///     A <see cref="CommandList" /> is needed to submit drawing and resource update commands.
    /// </summary>
    public void Render()
    {
        if (_frameBegun)
        {
            var oldCtx = ImGui.GetCurrentContext();

            if (oldCtx != Context) ImGui.SetCurrentContext(Context);

            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData());

            if (oldCtx != Context) ImGui.SetCurrentContext(oldCtx);
        }
    }

    /// <summary>
    ///     Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        var oldCtx = ImGui.GetCurrentContext();

        if (oldCtx != Context) ImGui.SetCurrentContext(Context);

        if (_frameBegun) ImGui.Render();

        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput();

        _frameBegun = true;
        ImGui.NewFrame();

        if (oldCtx != Context) ImGui.SetCurrentContext(oldCtx);
    }

    /// <summary>
    ///     Sets per-frame data based on the associated window.
    ///     This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_windowWidth, _windowHeight);

        if (_windowWidth > 0 && _windowHeight > 0)
            io.DisplayFramebufferScale = new Vector2(_windowWidth,
                _windowHeight);

        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private void UpdateImGuiInput()
    {
        var io = ImGui.GetIO();

        Glfw.GetCursorPosition(GlfwWindow, out var mouseX, out var mouseY);

        io.MouseDown[0] = Glfw.GetMouseButton(GlfwWindow, MouseButton.Left) == InputState.Press;
        io.MouseDown[1] = Glfw.GetMouseButton(GlfwWindow, MouseButton.Right) == InputState.Press;
        io.MouseDown[2] = Glfw.GetMouseButton(GlfwWindow, MouseButton.Middle) == InputState.Press;

        var point = new Point((int)mouseX, (int)mouseY);
        io.MousePos = new Vector2(point.X, point.Y);

        foreach (var c in _pressedChars) io.AddInputCharacter(c);

        _pressedChars.Clear();

        io.KeyCtrl = Glfw.GetKey(GlfwWindow, Keys.LeftControl) == InputState.Press ||
                     Glfw.GetKey(GlfwWindow, Keys.RightControl) == InputState.Press;

        io.KeyAlt = Glfw.GetKey(GlfwWindow, Keys.LeftAlt) == InputState.Press ||
                    Glfw.GetKey(GlfwWindow, Keys.RightAlt) == InputState.Press;

        io.KeyShift = Glfw.GetKey(GlfwWindow, Keys.LeftShift) == InputState.Press ||
                      Glfw.GetKey(GlfwWindow, Keys.RightShift) == InputState.Press;

        io.KeySuper = Glfw.GetKey(GlfwWindow, Keys.LeftSuper) == InputState.Press ||
                      Glfw.GetKey(GlfwWindow, Keys.RightSuper) == InputState.Press;
    }

    internal void PressChar(char keyChar)
    {
        _pressedChars.Add(keyChar);
    }

    /// <summary>
    ///     Translates a GLFW "Keys" to an ImGuiKey.
    /// </summary>
    /// <param name="key">The Keys to translate.</param>
    /// <returns>The corresponding ImGuiKey.</returns>
    /// <exception cref="NotImplementedException">When the key has not been implemented yet.</exception>
    private static ImGuiKey TranslateInputKeyToImGuiKey(Keys key)
    {
        return key switch
        {
            Keys.Tab => ImGuiKey.Tab,
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.Insert => ImGuiKey.Insert,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Backspace => ImGuiKey.Backspace,
            Keys.Space => ImGuiKey.Space,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Apostrophe => ImGuiKey.Apostrophe,
            Keys.Comma => ImGuiKey.Comma,
            Keys.Minus => ImGuiKey.Minus,
            Keys.Period => ImGuiKey.Period,
            Keys.Slash => ImGuiKey.Slash,
            Keys.SemiColon => ImGuiKey.Semicolon,
            Keys.Equal => ImGuiKey.Equal,
            Keys.LeftBracket => ImGuiKey.LeftBracket,
            Keys.Backslash => ImGuiKey.Backslash,
            Keys.RightBracket => ImGuiKey.RightBracket,
            Keys.GraveAccent => ImGuiKey.GraveAccent,
            Keys.CapsLock => ImGuiKey.CapsLock,
            Keys.ScrollLock => ImGuiKey.ScrollLock,
            Keys.NumLock => ImGuiKey.NumLock,
            Keys.PrintScreen => ImGuiKey.PrintScreen,
            Keys.Pause => ImGuiKey.Pause,
            Keys.Numpad0 => ImGuiKey.Keypad0,
            Keys.Numpad1 => ImGuiKey.Keypad1,
            Keys.Numpad2 => ImGuiKey.Keypad2,
            Keys.Numpad3 => ImGuiKey.Keypad3,
            Keys.Numpad4 => ImGuiKey.Keypad4,
            Keys.Numpad5 => ImGuiKey.Keypad5,
            Keys.Numpad6 => ImGuiKey.Keypad6,
            Keys.Numpad7 => ImGuiKey.Keypad7,
            Keys.Numpad8 => ImGuiKey.Keypad8,
            Keys.Numpad9 => ImGuiKey.Keypad9,
            Keys.NumpadDecimal => ImGuiKey.KeypadDecimal,
            Keys.NumpadDivide => ImGuiKey.KeypadDivide,
            Keys.NumpadMultiply => ImGuiKey.KeypadMultiply,
            Keys.NumpadSubtract => ImGuiKey.KeypadSubtract,
            Keys.NumpadAdd => ImGuiKey.KeypadAdd,
            Keys.NumpadEnter => ImGuiKey.KeypadEnter,
            Keys.NumpadEqual => ImGuiKey.KeypadEqual,
            Keys.LeftShift => ImGuiKey.LeftShift,
            Keys.LeftControl => ImGuiKey.LeftCtrl,
            Keys.LeftAlt => ImGuiKey.LeftAlt,
            Keys.LeftSuper => ImGuiKey.LeftSuper,
            Keys.RightShift => ImGuiKey.RightShift,
            Keys.RightControl => ImGuiKey.RightCtrl,
            Keys.RightAlt => ImGuiKey.RightAlt,
            Keys.RightSuper => ImGuiKey.RightSuper,
            Keys.Menu => ImGuiKey.Menu,
            Keys.Alpha0 => ImGuiKey._0,
            Keys.Alpha1 => ImGuiKey._1,
            Keys.Alpha2 => ImGuiKey._2,
            Keys.Alpha3 => ImGuiKey._3,
            Keys.Alpha4 => ImGuiKey._4,
            Keys.Alpha5 => ImGuiKey._5,
            Keys.Alpha6 => ImGuiKey._6,
            Keys.Alpha7 => ImGuiKey._7,
            Keys.Alpha8 => ImGuiKey._8,
            Keys.Alpha9 => ImGuiKey._9,
            Keys.A => ImGuiKey.A,
            Keys.B => ImGuiKey.B,
            Keys.C => ImGuiKey.C,
            Keys.D => ImGuiKey.D,
            Keys.E => ImGuiKey.E,
            Keys.F => ImGuiKey.F,
            Keys.G => ImGuiKey.G,
            Keys.H => ImGuiKey.H,
            Keys.I => ImGuiKey.I,
            Keys.J => ImGuiKey.J,
            Keys.K => ImGuiKey.K,
            Keys.L => ImGuiKey.L,
            Keys.M => ImGuiKey.M,
            Keys.N => ImGuiKey.N,
            Keys.O => ImGuiKey.O,
            Keys.P => ImGuiKey.P,
            Keys.Q => ImGuiKey.Q,
            Keys.R => ImGuiKey.R,
            Keys.S => ImGuiKey.S,
            Keys.T => ImGuiKey.T,
            Keys.U => ImGuiKey.U,
            Keys.V => ImGuiKey.V,
            Keys.W => ImGuiKey.W,
            Keys.X => ImGuiKey.X,
            Keys.Y => ImGuiKey.Y,
            Keys.Z => ImGuiKey.Z,
            Keys.F1 => ImGuiKey.F1,
            Keys.F2 => ImGuiKey.F2,
            Keys.F3 => ImGuiKey.F3,
            Keys.F4 => ImGuiKey.F4,
            Keys.F5 => ImGuiKey.F5,
            Keys.F6 => ImGuiKey.F6,
            Keys.F7 => ImGuiKey.F7,
            Keys.F8 => ImGuiKey.F8,
            Keys.F9 => ImGuiKey.F9,
            Keys.F10 => ImGuiKey.F10,
            Keys.F11 => ImGuiKey.F11,
            Keys.F12 => ImGuiKey.F12,
            Keys.F13 => ImGuiKey.F13,
            Keys.F14 => ImGuiKey.F14,
            Keys.F15 => ImGuiKey.F15,
            Keys.F16 => ImGuiKey.F16,
            Keys.F17 => ImGuiKey.F17,
            Keys.F18 => ImGuiKey.F18,
            Keys.F19 => ImGuiKey.F19,
            Keys.F20 => ImGuiKey.F20,
            Keys.F21 => ImGuiKey.F21,
            Keys.F22 => ImGuiKey.F22,
            Keys.F23 => ImGuiKey.F23,
            Keys.F24 => ImGuiKey.F24,
            _ => throw new NotImplementedException()
        };
    }

    private unsafe void SetupRenderState(ImDrawDataPtr drawDataPtr, int framebufferWidth, int framebufferHeight)
    {
        // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
        _gl.Enable(GLEnum.Blend);
        _gl.BlendEquation(GLEnum.FuncAdd);
        _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
        _gl.Disable(GLEnum.CullFace);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(GLEnum.StencilTest);
        _gl.Enable(GLEnum.ScissorTest);
        _gl.Disable(GLEnum.PrimitiveRestart);
        _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);

        var L = drawDataPtr.DisplayPos.X;
        var R = drawDataPtr.DisplayPos.X + drawDataPtr.DisplaySize.X;
        var T = drawDataPtr.DisplayPos.Y;
        var B = drawDataPtr.DisplayPos.Y + drawDataPtr.DisplaySize.Y;

        Span<float> orthoProjection = stackalloc float[]
        {
            2.0f / (R - L), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (T - B), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T), 0.0f, 1.0f
        };

        _shader.UseShader();
        _gl.Uniform1(_attribLocationTex, 0);
        _gl.UniformMatrix4(_attribLocationProjMtx, 1, false, orthoProjection);
        _gl.CheckGlError("Projection");

        _gl.BindSampler(0, 0);

        // Setup desired GL state
        // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
        // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
        _vertexArrayObject = _gl.GenVertexArray();
        _gl.BindVertexArray(_vertexArrayObject);
        _gl.CheckGlError("VAO");

        // Bind vertex/index buffers and setup attributes for ImDrawVert
        _gl.BindBuffer(GLEnum.ArrayBuffer, _vboHandle);
        _gl.BindBuffer(GLEnum.ElementArrayBuffer, _elementsHandle);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxUV);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        _gl.VertexAttribPointer((uint)_attribLocationVtxPos, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert),
            (void*)0);
        _gl.VertexAttribPointer((uint)_attribLocationVtxUV, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)8);
        _gl.VertexAttribPointer((uint)_attribLocationVtxColor, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert),
            (void*)16);
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr drawDataPtr)
    {
        var framebufferWidth = (int)drawDataPtr.DisplaySize.X;
        var framebufferHeight = (int)drawDataPtr.DisplaySize.Y;
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
            return;

        // Backup GL state
        _gl.GetInteger(GLEnum.ActiveTexture, out var lastActiveTexture);
        _gl.ActiveTexture(GLEnum.Texture0);

        _gl.GetInteger(GLEnum.CurrentProgram, out var lastProgram);
        _gl.GetInteger(GLEnum.TextureBinding2D, out var lastTexture);

        _gl.GetInteger(GLEnum.SamplerBinding, out var lastSampler);

        _gl.GetInteger(GLEnum.ArrayBufferBinding, out var lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out var lastVertexArrayObject);

        Span<int> lastPolygonMode = stackalloc int[2];
        _gl.GetInteger(GLEnum.PolygonMode, lastPolygonMode);

        Span<int> lastScissorBox = stackalloc int[4];
        _gl.GetInteger(GLEnum.ScissorBox, lastScissorBox);

        _gl.GetInteger(GLEnum.BlendSrcRgb, out var lastBlendSrcRgb);
        _gl.GetInteger(GLEnum.BlendDstRgb, out var lastBlendDstRgb);

        _gl.GetInteger(GLEnum.BlendSrcAlpha, out var lastBlendSrcAlpha);
        _gl.GetInteger(GLEnum.BlendDstAlpha, out var lastBlendDstAlpha);

        _gl.GetInteger(GLEnum.BlendEquationRgb, out var lastBlendEquationRgb);
        _gl.GetInteger(GLEnum.BlendEquationAlpha, out var lastBlendEquationAlpha);

        var lastEnableBlend = _gl.IsEnabled(GLEnum.Blend);
        var lastEnableCullFace = _gl.IsEnabled(GLEnum.CullFace);
        var lastEnableDepthTest = _gl.IsEnabled(GLEnum.DepthTest);
        var lastEnableStencilTest = _gl.IsEnabled(GLEnum.StencilTest);
        var lastEnableScissorTest = _gl.IsEnabled(GLEnum.ScissorTest);

        var lastEnablePrimitiveRestart = _gl.IsEnabled(GLEnum.PrimitiveRestart);

        SetupRenderState(drawDataPtr, framebufferWidth, framebufferHeight);

        // Will project scissor/clipping rectangles into framebuffer space
        var clipOff = drawDataPtr.DisplayPos;
        var clipScale = Vector2.One;

        // Render command lists
        for (var n = 0; n < drawDataPtr.CmdListsCount; n++)
        {
            var cmdListPtr = drawDataPtr.CmdLists[n];

            // Upload vertex/index buffers

            _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(cmdListPtr.VtxBuffer.Size * sizeof(ImDrawVert)),
                (void*)cmdListPtr.VtxBuffer.Data, GLEnum.StreamDraw);
            _gl.CheckGlError($"Data Vert {n}");
            _gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(cmdListPtr.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdListPtr.IdxBuffer.Data, GLEnum.StreamDraw);
            _gl.CheckGlError($"Data Idx {n}");

            for (var cmd_i = 0; cmd_i < cmdListPtr.CmdBuffer.Size; cmd_i++)
            {
                var cmdPtr = cmdListPtr.CmdBuffer[cmd_i];

                if (cmdPtr.UserCallback != IntPtr.Zero) throw new NotImplementedException();

                Vector4 clipRect;
                clipRect.X = (cmdPtr.ClipRect.X - clipOff.X) * clipScale.X;
                clipRect.Y = (cmdPtr.ClipRect.Y - clipOff.Y) * clipScale.Y;
                clipRect.Z = (cmdPtr.ClipRect.Z - clipOff.X) * clipScale.X;
                clipRect.W = (cmdPtr.ClipRect.W - clipOff.Y) * clipScale.Y;

                if (clipRect.X < framebufferWidth && clipRect.Y < framebufferHeight && clipRect.Z >= 0.0f &&
                    clipRect.W >= 0.0f)
                {
                    // Apply scissor/clipping rectangle
                    _gl.Scissor((int)clipRect.X, (int)(framebufferHeight - clipRect.W), (uint)(clipRect.Z - clipRect.X),
                        (uint)(clipRect.W - clipRect.Y));
                    _gl.CheckGlError("Scissor");

                    // Bind texture, Draw
                    _gl.BindTexture(GLEnum.Texture2D, (uint)cmdPtr.TextureId);
                    _gl.CheckGlError("Texture");

                    _gl.DrawElementsBaseVertex(GLEnum.Triangles, cmdPtr.ElemCount, GLEnum.UnsignedShort,
                        (void*)(cmdPtr.IdxOffset * sizeof(ushort)), (int)cmdPtr.VtxOffset);
                    _gl.CheckGlError("Draw");
                }
            }
        }

        // Destroy the temporary VAO
        _gl.DeleteVertexArray(_vertexArrayObject);
        _vertexArrayObject = 0;

        // Restore modified GL state
        _gl.UseProgram((uint)lastProgram);
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);

        _gl.BindSampler(0, (uint)lastSampler);

        _gl.ActiveTexture((GLEnum)lastActiveTexture);

        _gl.BindVertexArray((uint)lastVertexArrayObject);

        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
        _gl.BlendEquationSeparate((GLEnum)lastBlendEquationRgb, (GLEnum)lastBlendEquationAlpha);
        _gl.BlendFuncSeparate((GLEnum)lastBlendSrcRgb, (GLEnum)lastBlendDstRgb, (GLEnum)lastBlendSrcAlpha,
            (GLEnum)lastBlendDstAlpha);

        if (lastEnableBlend)
            _gl.Enable(GLEnum.Blend);
        else
            _gl.Disable(GLEnum.Blend);

        if (lastEnableCullFace)
            _gl.Enable(GLEnum.CullFace);
        else
            _gl.Disable(GLEnum.CullFace);

        if (lastEnableDepthTest)
            _gl.Enable(GLEnum.DepthTest);
        else
            _gl.Disable(GLEnum.DepthTest);

        if (lastEnableStencilTest)
            _gl.Enable(GLEnum.StencilTest);
        else
            _gl.Disable(GLEnum.StencilTest);

        if (lastEnableScissorTest)
            _gl.Enable(GLEnum.ScissorTest);
        else
            _gl.Disable(GLEnum.ScissorTest);

        if (lastEnablePrimitiveRestart)
            _gl.Enable(GLEnum.PrimitiveRestart);
        else
            _gl.Disable(GLEnum.PrimitiveRestart);

        _gl.PolygonMode(GLEnum.FrontAndBack, (GLEnum)lastPolygonMode[0]);
        _gl.Scissor(lastScissorBox[0], lastScissorBox[1], (uint)lastScissorBox[2], (uint)lastScissorBox[3]);
        _gl.CheckGlError("Render ImGui draw data");
    }

    private void CreateDeviceResources()
    {
        _gl.GetInteger(GLEnum.TextureBinding2D, out var lastTexture);
        _gl.GetInteger(GLEnum.ArrayBufferBinding, out var lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out var lastVertexArray);

        var vertexSource =
            @"#version 330
        layout (location = 0) in vec2 Position;
        layout (location = 1) in vec2 UV;
        layout (location = 2) in vec4 Color;
        uniform mat4 ProjMtx;
        out vec2 Frag_UV;
        out vec4 Frag_Color;
        void main()
        {
            Frag_UV = UV;
            Frag_Color = Color;
            gl_Position = ProjMtx * vec4(Position.xy,0,1);
        }";

        var fragmentSource =
            @"#version 330
        in vec2 Frag_UV;
        in vec4 Frag_Color;
        uniform sampler2D Texture;
        layout (location = 0) out vec4 Out_Color;
        void main()
        {
            Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
        }";

        _shader = new ImGuiShader(_gl, vertexSource, fragmentSource);

        _attribLocationTex = _shader.GetUniformLocation("Texture");
        _attribLocationProjMtx = _shader.GetUniformLocation("ProjMtx");
        _attribLocationVtxPos = _shader.GetAttribLocation("Position");
        _attribLocationVtxUV = _shader.GetAttribLocation("UV");
        _attribLocationVtxColor = _shader.GetAttribLocation("Color");

        _vboHandle = _gl.GenBuffer();
        _elementsHandle = _gl.GenBuffer();

        RecreateFontDeviceTexture();

        // Restore modified GL state
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);

        _gl.BindVertexArray((uint)lastVertexArray);

        _gl.CheckGlError("End of ImGui setup");
    }

    /// <summary>
    ///     Creates the texture used to render text.
    /// </summary>
    private void RecreateFontDeviceTexture()
    {
        // Build texture atlas
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var width, out var height,
            out var bytesPerPixel); // Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders. If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory.

        // Upload texture to graphics system
        _gl.GetInteger(GLEnum.TextureBinding2D, out var lastTexture);

        _fontTexture = new ImGuiTexture(_gl, width, height, pixels);
        _fontTexture.Bind();
        _fontTexture.SetMagFilter(TextureMagFilter.Linear);
        _fontTexture.SetMinFilter(TextureMinFilter.Linear);

        // Store our identifier
        io.Fonts.SetTexID((IntPtr)_fontTexture.GlTexture);

        // Restore state
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        io.Fonts.ClearTexData();
    }
}