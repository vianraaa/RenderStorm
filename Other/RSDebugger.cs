using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStorm.RSImGui;
using Silk.NET.OpenGL;

namespace RenderStorm.Other;

public static class RSDebugger
{
    internal static List<IProfilerObject> Buffers = new();
    internal static List<IProfilerObject> VertexArrays = new();
    internal static List<IProfilerObject> Textures = new();
    internal static List<IProfilerObject> Shaders = new();
    internal static List<IProfilerObject> RenderTextures = new();

    private static int _profilerTab;
    private static Dictionary<string, Action> _profilerTabs = new()
    {
        { "Overview", DrawOverviewTab },
        { "Resources", DrawResourcesTab }
    };
    private static readonly Vector4 _activeTabColor = new(0.26f, 0.59f, 0.98f, 0.4f);
    private static readonly Vector4 _inactiveTabColor = new(0.3f, 0.3f, 0.3f, 0.4f);
    public static int BufferCount { get; internal set; }
    public static int VertexArrayCount { get; internal set; }
    public static int TextureCount { get; internal set; }
    public static int ShaderCount { get; internal set; }
    public static int RenderTextureCount { get; internal set; }
    public static double TimeElapsed { get; internal set; } = 0;
    public static double DeltaTime { get; internal set; }
    public const string RSVERSION = "RENDERSTORM 0.1.1";

    private static RSRenderTexture? _arrayTexture;
    private static RSShader? _arrayShader;

    private static float _previewCamZoom = 1;
    private static float _previewCamZoomTarget = 1;

    private static void onScroll(float delta)
    {
        if(!ImGuiController.CanScroll)
            _previewCamZoomTarget -= delta / 10f;
    }

    public static void Init(RSWindow window)
    {
        ImGuiController.OnScroll += onScroll;
        _arrayTexture = new RSRenderTexture(256, 256, window, "ArrayObjectPreviewBuffer");
        RenderTextures.Remove(_arrayTexture);
        RenderTextureCount -= 1;
        _arrayTexture.Begin();
        _arrayTexture.End();
        string vertexShaderSource = @"
#version 330 core
layout(location = 0) in vec3 POS;

uniform mat4 view;
uniform mat4 project;

out vec3 fragColor;

uint hash(uint x) {
    x ^= x >> 16;
    x *= uint(0x7feb352d);
    x ^= x >> 15;
    x *= uint(0x846ca68b);
    x ^= x >> 16;
    return x;
}

vec3 idToColor(int id) {
    uint h = hash(uint(id));
    float r = float((h & 0xFF0000u) >> uint(16)) / 255.0f;
    float g = float((h & 0x00FF00u) >> uint(8)) / 255.0f;
    float b = float((h & 0x0000FFu)) / 255.0f;
    return vec3(r, g, b);
}

void main() {
    fragColor = idToColor(gl_VertexID);
    vec4 worldPos = vec4(POS, 1.0);
    gl_Position = project * view * worldPos;
}";
        string fragmentShaderSource = @"
#version 330 core

in vec3 fragColor;
out vec4 FragColor;

void main() {
    FragColor = vec4(fragColor, 1.0);
}";
        _arrayShader = new RSShader(vertexShaderSource, fragmentShaderSource, "ArrayObjectPreviewShader");
        Shaders.Remove(_arrayShader);
        ShaderCount -= 1;
    }

    public static void Dispose()
    {
        ImGuiController.OnScroll -= onScroll;
        _arrayTexture?.Dispose();
        // _arrayShader?.Dispose(); // shader lifetime is managed by the engine
    }
    
    // Don't use this for ANYTHING other than debug, It is slow and inefficient. 
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

    public static void DrawDebugger()
    {
        _previewCamZoom = float.Lerp(_previewCamZoom, _previewCamZoomTarget, (float)DeltaTime * 8f);
        ImGui.SetNextWindowSize(new Vector2(430,328), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(42,32), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(430,328), new Vector2(1000,500));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        if (ImGui.Begin("RenderStorm Debugger", ImGuiWindowFlags.NoSavedSettings))
        {
            DrawTabBar();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 12));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
            _profilerTabs.ElementAt(_profilerTab).Value.Invoke();
            ImGui.PopStyleVar(2);
        }
        ImGui.End();

        ImGui.PopStyleVar();
    }

    public static void PushCustomMenu(string name, Action layoutFunction)
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

    private static void DrawOverviewTab()
    {
        ImGui.Spacing();
        DrawPerformanceSection();
        ImGui.Spacing();
        DrawResourceSummarySection();
    }

    private static void DrawResourcesTab()
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
                var buf = obj as IDrawableArray;
                ImGui.BeginChild($"{obj.NativeInstance}", new Vector2(ImGui.GetContentRegionAvail().X, 0),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.FrameStyle);
                ImGui.Text($"{obj.DebugName}({obj.NativeInstance})");
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X -
                                    ImGui.CalcTextSize($"A: {buf.VertexBufferIndex} | B: {buf.IndexBufferIndex}").X );
                ImGui.Text($"A: {buf.VertexBufferIndex} | B: {buf.IndexBufferIndex}");
                ImGui.EndChild();
                if (ImGui.BeginItemTooltip())
                {
                    ImGuiController.CanScroll = false;
                    ImGui.SeparatorText(obj.DebugName);
                    _arrayTexture.Begin();
                    _arrayShader.Use();
                    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(1.6f, 1, 0.01f, 1000);
                    float angle = (float)TimeElapsed;
                    Vector3 eye = Vector3.Transform(new Vector3(0, 0.5f, 1) * _previewCamZoom, Matrix4x4.CreateRotationY(angle));
                    Matrix4x4 view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
                    _arrayShader.SetUniform("view", view);
                    _arrayShader.SetUniform("project", projection);
                    OpenGL.DepthTest = true;
                    OpenGL.CullFace = false;
                    buf.DrawIndexed();
                    _arrayTexture.End();
                    ImGui.Image((int)_arrayTexture?.ColorTexture, new Vector2(256, 256), new(0, 1), new(1, 0));
                    ImGui.Text($"Vertex Buffer: {buf.VertexBufferName}");
                    ImGui.Text($"Index Buffer: {buf.IndexBufferName}");
                    ImGui.EndTooltip();
                }
                else
                {
                    ImGuiController.CanScroll = true;
                }
            }

            ImGui.PopStyleVar();
            ImGui.Spacing();
        }
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
                    ImGui.Text($"Target: \"{Enum.GetName(buf.Target)}\"");
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
                    ImGui.Image((IntPtr)texture.NativeInstance, new Vector2(size, size));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName);
                        ImGui.Image((IntPtr)texture.NativeInstance, new(256, 256));
                        ImGui.Text($"{texture.Width}x{texture.Height}   ");
                        ImGui.SameLine();
                        ImGui.Text($"Tiled: {texture.CreationSettings.IsTiled}   ");
                        ImGui.SameLine();
                        ImGui.Text($"Mipmaps: {texture.CreationSettings.HasMipmaps}");
                        ImGui.Text($"Filter Mode: \"{Enum.GetName(texture.CreationSettings.Filtering)}\"");
                        ImGui.Text($"Pixel Format: \"{Enum.GetName(texture.CreationSettings.Format)}\"");
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
        if (ImGui.CollapsingHeader("Render Textures", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (RenderTextures.Count == 0)
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("empty. this is awkward...").X) * 0.5f);
                ImGui.Text("empty. this is awkward...");
            }
            const int size = 128;
            var columns = (int)Math.Floor(ImGui.GetContentRegionAvail().X / size);
            var rowCount = (int)Math.Ceiling((float)RenderTextures.Count / columns);
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= RenderTextures.Count)
                        break;
                    var texture = (RSRenderTexture)RenderTextures[index];
                    ImGui.Image((IntPtr)texture.ColorTexture, new Vector2(size, size), new(0, 1), new(1, 0));
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.SeparatorText(texture.DebugName);
                        float maxSize = 512f;
                        float aspect = (float)texture.Width / texture.Height;

                        Vector2 sizear = aspect >= 1.0f
                            ? new Vector2(maxSize, maxSize / aspect)
                            : new Vector2(maxSize * aspect, maxSize);

                        ImGui.Image((IntPtr)texture.ColorTexture, sizear, new(0, 1), new(1, 0));
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
        ImGui.Text("Frame Time:");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{deltaTimeMs:F2} ms").X);
        ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), $"{deltaTimeMs:F2} ms");
        var fps = DeltaTime > 0 ? 1.0f / (float)DeltaTime : 0;
        ImGui.Text("FPS:");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{fps:F0}").X);
        var fpsColor = fps switch
        {
            >= 60 => new Vector4(0, 1, 0, 1),
            >= 30 => new Vector4(1, 1, 0, 1),
            _ => new Vector4(1, 0, 0, 1)
        };
        ImGui.TextColored(fpsColor, $"{fps:F0}");
    }

    private static void DrawResourceSummarySection()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Resource count");
        ImGui.Separator();
        DrawResourceItem("Buffers", BufferCount);
        DrawResourceItem("Vertex Arrays", VertexArrayCount);
        DrawResourceItem("Textures", TextureCount);
        DrawResourceItem("Shaders", ShaderCount);
        DrawResourceItem("Render Textures", RenderTextureCount);
    }

    private static void DrawResourceItem(string label, int count)
    {
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize($"{count}").X);
        ImGui.Text($"{count}");
    }
}