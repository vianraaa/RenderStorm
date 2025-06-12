using System.Drawing;
using System.Numerics;
using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStormImpl;

namespace RenderStorm.Other;

public struct DebugAttribs
{
    public Vector3 POSITION;
}
public struct DebugConstantBuffer
{
    public Matrix4x4 worldViewProjection;
}

public static class RSDebugger
{
    internal static List<IProfilerObject> Buffers = new();
    internal static List<IProfilerObject> VertexArrays = new();
    internal static List<IProfilerObject> Textures = new();
    internal static List<IProfilerObject> Shaders = new();
    internal static List<IProfilerObject> RenderTargets = new();
    
    private static List<IProfilerObject> _buffersSearchCache = new();
    private static List<IProfilerObject> _vertexArraysSearchCache = new();
    private static List<IProfilerObject> _texturesSearchCache = new();
    private static List<IProfilerObject> _renderTargetsSearchCache = new();



    private static int selectedQueue = 0;

    private static int _profilerTab;
    private static Dictionary<string, Action> _profilerTabs = new()
    {
        { "Overview", DrawOverviewTab },
        { "Resources", DrawResourcesTab }
    };
    private static readonly Vector4 _activeTabColor = new(0.26f, 0.59f, 0.98f, 0.4f);
    private static readonly Vector4 _inactiveTabColor = new(0.3f, 0.3f, 0.3f, 0.4f);
    public static double TimeElapsed { get; internal set; } = 0;
    public static double DeltaTime { get; internal set; }
    public const string RSVERSION = "RENDERSTORM 0.1.3";
    public static RSWindow RSWindow { get; private set; }
    private static RSRenderTarget? _arrayTexture;
    private static RSShader? _arrayShader;
    private static string _resourceSearchTerm = "";

    private static float _previewCamZoom = 1;
    private static float _previewCamZoomTarget = 1;
    private static DebugConstantBuffer _previewConstantBuffer = new();

    private static void onScroll(float delta)
    {
        if(!ImGuiSdlInput.CanScroll)
            _previewCamZoomTarget -= delta / 10f;
    }
    public static void Init(RSWindow window)
    {
        ImGuiSdlInput.OnScroll += onScroll;
        _arrayTexture = new RSRenderTarget(256, 256);
        RenderTargets.Remove(_arrayTexture);
        _arrayTexture?.Begin();
        _arrayTexture?.End();
        _arrayShader = new RSShader<DebugAttribs>(@"
cbuffer DebugConstantBuffer : register(b0)
{
    row_major float4x4 worldViewProjection;
}

struct VertexIn
{
    float3 POSITION : POSITION;
};

struct VertexOut
{
    float4 POSITION : SV_POSITION;
    float3 COLOR : COLOR;
};

uint hash(uint x) {
    x ^= x >> 16;
    x *= uint(0x7feb352d);
    x ^= x >> 15;
    x *= uint(0x846ca68b);
    x ^= x >> 16;
    return x;
}

float3 idToColor(int id) {
    uint h = hash(uint(id));
    float r = float((h & 0xFF0000u) >> uint(16)) / 255.0f;
    float g = float((h & 0x00FF00u) >> uint(8)) / 255.0f;
    float b = float((h & 0x0000FFu)) / 255.0f;
    return float3(r, g, b);
}

VertexOut vert(VertexIn input, uint vertexID : SV_VertexID)
{
    VertexOut output;

    output.POSITION = mul(float4(input.POSITION, 1.0f), worldViewProjection);
    output.COLOR = idToColor(vertexID);

    return output;
}

float4 frag(VertexOut input) : SV_Target
{
    return float4(input.COLOR, 1.0f);
}
");
        Shaders.Remove(_arrayShader);
        RSWindow = window;
    }

    public static void Dispose()
    {
        ImGuiSdlInput.OnScroll -= onScroll;
        _arrayTexture?.Dispose();
        _arrayShader?.Dispose();
    }

    /// <summary>
    /// Don't use this for ANYTHING other than debug, It is slow and inefficient.
    /// </summary>
    /// <param name="text">Text to be rendered</param>
    /// <param name="centerHorizontally"></param>
    /// <param name="position">The position of the text relative to the top left corner</param>
    /// <param name="color">The color of the text</param>
    /// <param name="bg"></param>
    public static void DrawDebugText(string text, bool centerHorizontally = false, Vector2? position = null, Color? color = null, bool bg = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var col = color ?? Color.White;
        var pos = position ?? Vector2.Zero;

        var drawList = bg ? ImGui.GetBackgroundDrawList() : ImGui.GetForegroundDrawList();
        var colU32 = ImGui.GetColorU32(new System.Numerics.Vector4(
            col.R / 255f,
            col.G / 255f,
            col.B / 255f,
            col.A / 255f
        ));

        var lines = text.Split('\n');
        float lineHeight = ImGui.GetTextLineHeight();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var textSize = ImGui.CalcTextSize(line);

            float x = pos.X;
            if (centerHorizontally)
            {
                x -= textSize.X / 2f;  // center each line horizontally around pos.X
            }

            float y = pos.Y + i * lineHeight;
            drawList.AddText(new Vector2(x, y), colU32, line);
        }
    }

    public static void DrawDebugRect(Vector2 position, Vector2 size, Color? color = null, bool bg = false)
    {
        var col = color ?? Color.White;

        var drawList = bg ? ImGui.GetBackgroundDrawList() : ImGui.GetForegroundDrawList();
        var colU32 = ImGui.GetColorU32(new System.Numerics.Vector4(
            col.R / 255f,
            col.G / 255f,
            col.B / 255f,
            col.A / 255f
        ));

        drawList.AddRectFilled(position, position + size, colU32);
    }
    
    public static void DrawDebugRectLines(Vector2 position, Vector2 size, Color? color = null, bool bg = false)
    {
        var col = color ?? Color.White;

        var drawList = bg ? ImGui.GetBackgroundDrawList() : ImGui.GetForegroundDrawList();
        var colU32 = ImGui.GetColorU32(new System.Numerics.Vector4(
            col.R / 255f,
            col.G / 255f,
            col.B / 255f,
            col.A / 255f
        ));

        drawList.AddRect(position, position + size, colU32);
    }
    
    private static string LimitText(string text, int maxLength = 40)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    public static void DrawDebugger()
    {
        ImGui.SetNextWindowPos(new Vector2(60,60), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(450,0), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        if (ImGui.Begin("RenderStorm Debugger", ImGuiWindowFlags.NoSavedSettings | 
                                                ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.SetWindowFocus("RenderStorm Debugger");
            DrawTabBar();
            _profilerTabs.ElementAt(_profilerTab).Value.Invoke();
        }
        ImGui.End();

        ImGui.PopStyleVar();
    }

    public static void PushCustomMenu(string name, Action? layoutFunction)
    {
        if (layoutFunction == null)
        {
            _profilerTabs.Remove(name);
            return;
        }
        _profilerTabs.Add(name, layoutFunction);
    }

    private static void DrawTabBar()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X / _profilerTabs.Count, 30);
        for (var i = 0; i < _profilerTabs.Count; i++)
        {
            var tab = _profilerTabs.ElementAt(i);
            ImGui.PushStyleColor(ImGuiCol.Button, _profilerTab == i ? _activeTabColor : _inactiveTabColor);
            if (ImGui.Button(tab.Key, buttonSize))
                _profilerTab = i;
            ImGui.PopStyleColor();
            ImGui.SameLine();
        }
        ImGui.PopStyleVar(2);
        ImGui.Spacing();
        ImGui.Separator();
    }

    private static void DrawOverviewTab()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 12));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
        ImGui.Spacing();
        DrawPerformanceSection();
        ImGui.Spacing();
        DrawResourceSummarySection();
        ImGui.PopStyleVar(2);
    }

    private static void DrawResourcesTab()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.Spacing();
        ImGui.InputTextWithHint("##resSearch", "Search...", ref _resourceSearchTerm, 256);
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Arrays"))
        {
            _vertexArraysSearchCache = VertexArrays.Where(b => b.DebugName.Contains(_resourceSearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
            if (_vertexArraysSearchCache.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            bool tooltipOpen = false;

            foreach (var obj in _vertexArraysSearchCache)
            {
                var buf = (IDrawableArray)obj;
                ImGui.BeginChild($"{obj.DebugName}{obj.NativeInstance}", new Vector2(ImGui.GetContentRegionAvail().X, 0),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.Text($"{LimitText(obj.DebugName)}({obj.NativeInstance})");
                ImGui.EndChild();

                if (ImGui.BeginItemTooltip())
                {
                    tooltipOpen = true;

                    _previewCamZoom = float.Lerp(_previewCamZoom, _previewCamZoomTarget, (float)(DeltaTime * 8));
                    ImGui.SeparatorText(obj.DebugName);

                    _arrayTexture.Begin();
                    _arrayShader.Use();

                    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(1.6f, 1, 0.01f, 1000);
                    float angle = (float)TimeElapsed;
                    Vector3 eye = Vector3.Transform(new Vector3(0, 0.5f, 1) * _previewCamZoom, Matrix4x4.CreateRotationY(angle));
                    Matrix4x4 view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
                    _previewConstantBuffer.worldViewProjection = view * projection;
                    _arrayShader.Use();
                    _arrayShader.SetCBuffer(0, _previewConstantBuffer);

                    buf.DrawIndexed();

                    _arrayTexture.End();

                    ImGui.Image(_arrayTexture.ColorShaderResourceView.NativePointer, new Vector2(256, 256));
                    ImGui.EndTooltip();
                }
            }

            ImGuiSdlInput.CanScroll = !tooltipOpen;

            ImGui.PopStyleVar();
            ImGui.Spacing();
        }
        if (ImGui.CollapsingHeader("Buffers"))
        {
            _buffersSearchCache = Buffers.Where(b => b.DebugName.Contains(_resourceSearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
            if (_buffersSearchCache.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            foreach (var obj in _buffersSearchCache)
            {
                var buf = obj as ITypedBuffer;
                ImGui.BeginChild($"{obj.NativeInstance}", new Vector2(ImGui.GetContentRegionAvail().X, 0),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.Text($"{LimitText(obj.DebugName)}({obj.NativeInstance})");
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X -
                                    ImGui.CalcTextSize($"{buf.Size} bytes").X);
                ImGui.Text($"{buf.Size} bytes");
                ImGui.EndChild();
                if (ImGui.BeginItemTooltip())
                {
                    ImGui.SeparatorText(obj.DebugName);
                    ImGui.Text($"Size: {buf.Size} bytes");
                    ImGui.Text($"Item Count: {buf.ItemCount}");
                    ImGui.Text($"Stored Type: \"{buf.StoredType.Name}\"");
                    ImGui.EndTooltip();
                }
            }

            ImGui.PopStyleVar();
            ImGui.Spacing();
        }
        if (ImGui.CollapsingHeader("Textures"))
        {
            _texturesSearchCache = Textures.Where(b => b.DebugName.Contains(_resourceSearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            if (_texturesSearchCache.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            const int size = 128;
            var columns = (int)Math.Floor(ImGui.GetContentRegionAvail().X / size);
            var rowCount = (int)Math.Ceiling((float)_texturesSearchCache.Count / columns);
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= _texturesSearchCache.Count)
                        break;
                    var texture = (RSTexture)_texturesSearchCache[index];
                    var drawList = ImGui.GetWindowDrawList();
                    var imagePos = ImGui.GetCursorScreenPos();
                    var imageSize = new Vector2(size, size);

                    ImGui.Image(texture.ShaderResourceView.NativePointer, imageSize);
                    if (ImGui.IsItemHovered())
                    {
                        drawList.AddRect(
                            imagePos,
                            imagePos + new Vector2(size, size),
                            ImGui.GetColorU32(new Vector4(1, 0, 0, 1)),
                            0.0f,
                            ImDrawFlags.None,
                            2.0f
                        );
                    }
                    
                    var label = LimitText(texture.DebugName ?? "<unnamed>", 15);
                    
                    var textSize = ImGui.CalcTextSize(label);
                    var textPos = new Vector2(
                        imagePos.X + (imageSize.X - textSize.X) * 0.5f,
                        imagePos.Y + (imageSize.Y - textSize.Y) * 0.5f
                    );

                    drawList.AddText(textPos + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 1)), label);
                    drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), label);


                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName ?? "<unnamed>");
                        ImGui.Image(texture.ShaderResourceView.NativePointer, new(256, 256));
                        ImGui.Text($"{texture.Width}x{texture.Height}   ");
                        ImGui.Text($"Mipmaps: {texture.Settings.HasMipmaps}");
                        ImGui.Text($"AddressMode: \"{Enum.GetName(texture.Settings.AddressMode)}\"");
                        ImGui.Text($"Filter Mode: \"{Enum.GetName(texture.Settings.Filtering)}\"");
                        ImGui.Text($"Pixel Format: \"{Enum.GetName(texture.Settings.Format)}\"");
                        ImGui.EndTooltip();
                    }

                    ImGui.PopStyleVar();
                    if (col < columns - 1)
                        ImGui.SameLine();
                }

            }
            ImGui.Spacing();
        }
        if (ImGui.CollapsingHeader("Render Targets"))
        {
            _renderTargetsSearchCache = RenderTargets.Where(b => b.DebugName.Contains(_resourceSearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            if (_renderTargetsSearchCache.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            const int size = 128;
            var columns = (int)Math.Floor(ImGui.GetContentRegionAvail().X / size);
            var rowCount = (int)Math.Ceiling((float)_renderTargetsSearchCache.Count / columns);
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= _renderTargetsSearchCache.Count)
                        break;
                    var texture = (RSRenderTarget)_renderTargetsSearchCache[index];
                    
                    var drawList = ImGui.GetWindowDrawList();
                    var imagePos = ImGui.GetCursorScreenPos();

                    if (texture.Type == RenderTargetType.DepthOnly || texture.ColorShaderResourceView == null)
                    {
                        if (texture.DepthShaderResourceView != null)
                        {
                            ImGui.Image(texture.DepthShaderResourceView.NativePointer, new Vector2(size, size));
                        }
                        else
                        {
                            ImGui.Dummy(new Vector2(size, size));
                            ImGui.Text("Depth Only");
                        }
                    }
                    else
                    {
                        ImGui.Image(texture.ColorShaderResourceView.NativePointer, new Vector2(size, size));
                    }
                    if (ImGui.IsItemHovered())
                    {
                        drawList.AddRect(
                            imagePos,
                            imagePos + new Vector2(size, size),
                            ImGui.GetColorU32(new Vector4(1, 0, 0, 1)),
                            0.0f,
                            ImDrawFlags.None,
                            2.0f
                        );
                    }
                    var imageSize = new Vector2(size, size);
                    var label = LimitText(texture.DebugName ?? "<unnamed>", 15);
                    var textSize = ImGui.CalcTextSize(label);
                    var textPos = new Vector2(
                        imagePos.X + (imageSize.X - textSize.X) * 0.5f,
                        imagePos.Y + (imageSize.Y - textSize.Y) * 0.5f
                    );
                    drawList.AddText(textPos + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 1)), label);
                    drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), label);

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName);
                        float maxSize = 512f;
                        float aspect = (float)texture.Width / texture.Height;

                        Vector2 sizear = aspect >= 1.0f
                            ? new Vector2(maxSize, maxSize / aspect)
                            : new Vector2(maxSize * aspect, maxSize);

                        if (texture.Type == RenderTargetType.DepthOnly || texture.ColorShaderResourceView == null)
                        {
                            if (texture.DepthShaderResourceView != null)
                            {
                                ImGui.Image((IntPtr)texture.DepthShaderResourceView.NativePointer, sizear);
                                ImGui.Text("Depth Texture");
                            }
                            else
                            {
                                ImGui.Dummy(sizear);
                                ImGui.Text("Depth Only (No Preview Available)");
                            }
                        }
                        else
                        {
                            ImGui.Image((IntPtr)texture.ColorShaderResourceView.NativePointer, sizear);
                        }
                        ImGui.Text($"Aspect: {aspect}");
                        ImGui.Text($"{texture.Width}x{texture.Height}");
                        ImGui.EndTooltip();
                    }

                    ImGui.PopStyleVar();
                    if (col < columns - 1)
                        ImGui.SameLine();
                }

                ImGui.Spacing();
            }
        }
    }

    private static void DrawPerformanceSection()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1f), "Performance");
        var deltaTimeMs = (float)DeltaTime * 1000;
        var fps = DeltaTime > 0 ? 1.0f / (float)DeltaTime : 0;
        ImGui.Text("Frame Time:");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{deltaTimeMs:F2} ms").X);
        var fpsColor = fps switch
        {
            >= 60 => new Vector4(0, 1, 0, 1),
            >= 30 => new Vector4(1, 1, 0, 1),
            _ => new Vector4(1, 0, 0, 1)
        };
        ImGui.TextColored(fpsColor, $"{deltaTimeMs:F2} ms");
        ImGui.Text(RSWindow.D3dDeviceContainer.VSync ? "FPS (Vsync):" : "FPS:");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{fps:F0}").X);
        ImGui.TextColored(fpsColor, $"{fps:F0}");
    }

    private static void DrawResourceSummarySection()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Resource count");
        ImGui.Separator();
        DrawResourceItem("Buffers", Buffers.Count);
        DrawResourceItem("Vertex Arrays", VertexArrays.Count);
        DrawResourceItem("Textures", Textures.Count);
        DrawResourceItem("Render Targets", RenderTargets.Count);
        DrawResourceItem("Shaders", Shaders.Count);
    }

    private static void DrawResourceItem(string label, int count)
    {
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{count}").X);
        ImGui.Text($"{count}");
    }
}