using System.Drawing;
using System.Numerics;
using ImGuiNET;
using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStorm.ImGuiImpl;

namespace RenderStorm.Other;

public struct DebugAttribs
{
    public Vector2 POSITION;
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
    

    private static int selectedQueue = 0;

    private static int _profilerTab;
    private static Dictionary<string, Action<D3D11DeviceContainer>> _profilerTabs = new()
    {
        { "Overview", DrawOverviewTab },
        { "Resources", DrawResourcesTab }
    };
    private static readonly Vector4 _activeTabColor = new(0.26f, 0.59f, 0.98f, 0.4f);
    private static readonly Vector4 _inactiveTabColor = new(0.3f, 0.3f, 0.3f, 0.4f);
    public static double TimeElapsed { get; internal set; } = 0;
    public static double DeltaTime { get; internal set; }
    public const string RSVERSION = "RENDERSTORM 0.1.1";
    public static RSWindow RSWindow { get; private set; }
    private static RSRenderTarget? _arrayTexture;
    private static RSShader? _arrayShader;

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
        _arrayTexture = new RSRenderTarget(window, 256, 256);
        RenderTargets.Remove(_arrayTexture);
        _arrayTexture?.Begin();
        _arrayTexture?.End();
        _arrayShader = new RSShader<DebugAttribs>(window.D3dDeviceContainer.Device, @"
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
    /// <param name="position">The position of the text relative to the top left corner</param>
    /// <param name="color">The color of the text</param>
    public static void DrawDebugText(string text, Vector2? position = null, Color? color = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        var col = color ?? Color.White;
        var pos = position ?? Vector2.Zero;
        
        var drawList = ImGui.GetForegroundDrawList();
        var colU32 = ImGui.GetColorU32(new System.Numerics.Vector4(
            col.R / 255f,
            col.G / 255f,
            col.B / 255f,
            col.A / 255f
        ));

        drawList.AddText(pos, colU32, text);
    }

    public static void DrawDebugger(D3D11DeviceContainer container)
    {
        ImGui.SetNextWindowSize(new Vector2(430,356), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(60,60), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(430,356), new Vector2(1000,500));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        if (ImGui.Begin("RenderStorm Debugger", ImGuiWindowFlags.NoSavedSettings))
        {
            DrawTabBar();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 12));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
            _profilerTabs.ElementAt(_profilerTab).Value.Invoke(container);
            ImGui.PopStyleVar(2);
        }
        ImGui.End();

        ImGui.PopStyleVar();
    }

    public static void PushCustomMenu(string name, Action<D3D11DeviceContainer> layoutFunction)
    {
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

    private static void DrawOverviewTab(D3D11DeviceContainer device)
    {
        ImGui.Spacing();
        DrawPerformanceSection();
        ImGui.Spacing();
        DrawResourceSummarySection();
    }

    private static void DrawResourcesTab(D3D11DeviceContainer device)
    {
        float x = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2f;
        const string msg = "The resources displayed are ONLY allocated by RenderStorm";
        x -= ImGui.CalcTextSize(msg).X/2;
        ImGui.SetCursorPosX(x);
        ImGui.Text(msg);
        if (ImGui.CollapsingHeader("Arrays", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
            if (VertexArrays.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            foreach (var obj in VertexArrays)
            {
                var buf = (IDrawableArray)obj;
                ImGui.BeginChild($"{obj.DebugName}{obj.NativeInstance}", new Vector2(ImGui.GetContentRegionAvail().X, 0),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.Text($"{obj.DebugName}({obj.NativeInstance})");
                ImGui.EndChild();
                if (ImGui.BeginItemTooltip())
                {
                    _previewCamZoom = float.Lerp(_previewCamZoom, _previewCamZoomTarget, (float)(DeltaTime * 8));
                    ImGuiSdlInput.CanScroll = false;
                    ImGui.SeparatorText(obj.DebugName);
                    _arrayTexture.Begin();
                    _arrayShader.Use(device);
                    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(1.6f, 1, 0.01f, 1000);
                    float angle = (float)TimeElapsed;
                    Vector3 eye = Vector3.Transform(new Vector3(0, 0.5f, 1) * _previewCamZoom, Matrix4x4.CreateRotationY(angle));
                    Matrix4x4 view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
                    _previewConstantBuffer.worldViewProjection = view * projection;
                    _arrayShader.Use(device);
                    _arrayShader.SetCBuffer(device, 0, _previewConstantBuffer);
                    buf.DrawIndexed(device);
                    _arrayTexture.End();
                    ImGui.Image((IntPtr)_arrayTexture?.ColorShaderResourceView.NativePointer, new Vector2(256, 256));
                    ImGui.EndTooltip();
                }
                else
                {
                    ImGuiSdlInput.CanScroll = true;
                }
            }

            ImGui.PopStyleVar();
            ImGui.Spacing();
        }
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Buffers", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
            if (Buffers.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            foreach (var obj in Buffers)
            {
                var buf = obj as ITypedBuffer;
                ImGui.BeginChild($"{obj.NativeInstance}", new Vector2(ImGui.GetContentRegionAvail().X, 0),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.Text($"{obj.DebugName}({obj.NativeInstance})");
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
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Textures", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (Textures.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            const int size = 128;
            var columns = (int)Math.Floor(ImGui.GetContentRegionAvail().X / size);
            var rowCount = (int)Math.Ceiling((float)Textures.Count / columns);
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= Textures.Count)
                        break;
                    var texture = (RSTexture)Textures[index];
                    ImGui.Image(texture.ShaderResourceView.NativePointer, new Vector2(size, size));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName);
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
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Render Targets", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (RenderTargets.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            const int size = 128;
            var columns = (int)Math.Floor(ImGui.GetContentRegionAvail().X / size);
            var rowCount = (int)Math.Ceiling((float)RenderTargets.Count / columns);
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= RenderTargets.Count)
                        break;
                    var texture = (RSRenderTarget)RenderTargets[index];
                    ImGui.Image(texture.ColorShaderResourceView.NativePointer, new Vector2(size, size));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName);
                        float maxSize = 512f;
                        float aspect = (float)texture.Width / texture.Height;

                        Vector2 sizear = aspect >= 1.0f
                            ? new Vector2(maxSize, maxSize / aspect)
                            : new Vector2(maxSize * aspect, maxSize);

                        ImGui.Image((IntPtr)texture.ColorShaderResourceView.NativePointer, sizear);
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
        
        ImGui.Spacing();
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