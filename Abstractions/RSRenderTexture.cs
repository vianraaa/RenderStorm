using System;
using GLFW;
using RenderStorm.Display;
using RenderStorm.Other;
using Silk.NET.OpenGL;
using Exception = System.Exception;

namespace RenderStorm.Abstractions;

public class RSRenderTexture : IProfilerObject, IDisposable
{
    private uint _colorTexture;
    private uint _depthTexture;
    private bool _disposed;
    private int _height;
    private int _width;
    private Window _nWindow;
    
    public int Width => _width;
    public int Height => _height;
    public uint ColorTexture => _colorTexture;
    public uint DepthTexture => _depthTexture;

    public RSRenderTexture(int width, int height, RSWindow win, string debugName = "RenderTexture")
    {
        _nWindow = win.Native;
        DebugName = debugName;
        _width = width;
        _height = height;
        CreateTextures();
        RSDebugger.RenderTextureCount++;
        RSDebugger.RenderTextures.Add(this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            RSDebugger.RenderTextures.Remove(this);
            RSDebugger.RenderTextureCount--;
            OpenGL.API.DeleteTextures(1, ref _colorTexture);
            OpenGL.API.DeleteTextures(1, ref _depthTexture);
            OpenGL.API.DeleteFramebuffers(1, ref NativeInstance);
            _disposed = true;
        }
    }

    private unsafe void CreateTextures()
    {
        OpenGL.API.GenTextures(1, out _colorTexture);
        OpenGL.API.BindTexture(TextureTarget.Texture2D, _colorTexture);
        OpenGL.API.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)_width, (uint)_height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, null);
        OpenGL.API.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);
        OpenGL.API.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear);

        OpenGL.API.GenTextures(1, out _depthTexture);
        OpenGL.API.BindTexture(TextureTarget.Texture2D, _depthTexture);
        OpenGL.API.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent32f, (uint)_width, (uint)_height,
            0, PixelFormat.DepthComponent, PixelType.Float, null);

        OpenGL.API.GenFramebuffers(1, out NativeInstance);
        OpenGL.API.BindFramebuffer(FramebufferTarget.Framebuffer, NativeInstance);
        OpenGL.API.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            _colorTexture, 0);
        OpenGL.API.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            _depthTexture, 0);

        if (OpenGL.API.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("Framebuffer is not complete");

        OpenGL.API.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Begin()
    {
        OpenGL.API.Viewport(0, 0, (uint)_width, (uint)_height);
        OpenGL.API.BindFramebuffer(FramebufferTarget.Framebuffer, NativeInstance);
        OpenGL.API.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        OpenGL.API.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void End()
    {
        OpenGL.API.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        Glfw.GetFramebufferSize(_nWindow, out var width, out var height);
        OpenGL.API.Viewport(0, 0, (uint)width, (uint)height);
    }

    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;

        OpenGL.API.DeleteTextures(1, ref _colorTexture);
        OpenGL.API.DeleteTextures(1, ref _depthTexture);
        OpenGL.API.DeleteFramebuffers(1, ref NativeInstance);

        CreateTextures();
    }
}