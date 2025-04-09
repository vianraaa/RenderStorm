using RenderStorm.Other;
using Silk.NET.OpenGL;

namespace RenderStorm.Abstractions;

public enum Filtering
{
    Linear,
    Nearest
}

public struct RSTextureCreationSettings(
    bool hasMipmaps = true,
    bool isTiled = true,
    Filtering filtering = Filtering.Linear,
    PixelFormat internalFormat = PixelFormat.Rgb)
{
    public bool HasMipmaps { get; set; } = hasMipmaps;
    public bool IsTiled { get; set; } = isTiled;
    public Filtering Filtering { get; set; } = filtering;
    public PixelFormat Format { get; set; } = internalFormat;
}

public class RSTexture : IProfilerObject, IDisposable
{
    private bool _disposed;

    public RSTexture(int width, int height, byte[] pixelData = null, RSTextureCreationSettings creationSettings = new(),
        string debugName = "Texture")
    {
        Width = width;
        Height = height;
        CreationSettings = creationSettings;
        DebugName = debugName;
        OpenGL.API.GenTextures(1, out NativeInstance);
        OpenGL.API.BindTexture(TextureTarget.Texture2D, NativeInstance);
        OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
        OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
        switch (creationSettings.Filtering)
        {
            case Filtering.Linear:
                if (creationSettings.HasMipmaps)
                    OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
                else
                    OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
                OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
                break;
            case Filtering.Nearest:
                if (creationSettings.HasMipmaps)
                    OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.NearestMipmapLinear);
                else
                    OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
                OpenGL.API.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
                break;
        }

        OpenGL.API.TexImage2D<byte>(GLEnum.Texture2D, 0, InternalFormat.Rgb, (uint)width, (uint)height, 0,
            creationSettings.Format, PixelType.UnsignedByte, pixelData);
        if (creationSettings.HasMipmaps) OpenGL.API.GenerateMipmap(GLEnum.Texture2D);

        RSDebugger.TextureCount++;
        RSDebugger.Textures.Add(this);
    }

    public RSTextureCreationSettings CreationSettings { get; }
    public int Width { get; }
    public int Height { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            RSDebugger.Textures.Remove(this);
            RSDebugger.TextureCount--;
            OpenGL.API.DeleteTextures(1, NativeInstance);
        }

        _disposed = true;
    }

    public void Bind(int unit)
    {
        OpenGL.API.ActiveTexture(TextureUnit.Texture0 + unit);
        OpenGL.API.BindTexture(TextureTarget.Texture2D, NativeInstance);
    }
}