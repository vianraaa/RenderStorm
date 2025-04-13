using System.Runtime.InteropServices;
using RenderStorm.Other;
using Vortice.Direct3D11;
using Vortice.DXGI;

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
    Format format = Format.R8G8B8A8_UNorm)
{
    public bool HasMipmaps { get; set; } = hasMipmaps;
    public bool IsTiled { get; set; } = isTiled;
    public Filtering Filtering { get; set; } = filtering;
    public Format Format { get; set; } = format;
}

public class RSTexture : IProfilerObject, IDisposable
{
    public uint Width { get; }
    public uint Height { get; }
    public RSTextureCreationSettings CreationSettings { get; }
    public ID3D11Texture2D Texture { get; private set; }
    public ID3D11ShaderResourceView ShaderResourceView { get; private set; }

    private bool _disposed;
    public RSTexture(ID3D11Device device, uint width, uint height, byte[] pixelData = null, RSTextureCreationSettings creationSettings = new(), string debugName = "Texture")
    {
        Width = width;
        Height = height;
        CreationSettings = creationSettings;

        var textureDesc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = creationSettings.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource | (creationSettings.HasMipmaps ? BindFlags.RenderTarget : 0),
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        };

        if (pixelData != null)
        {
            var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
            uint pitch = sizeof(byte) * width * 4; // Assuming 4 bytes per pixel (R8G8B8A8)
            var dataBox = new SubresourceData(handle.AddrOfPinnedObject(), pitch);
            Texture = device.CreateTexture2D(textureDesc, new[] { dataBox });
            handle.Free();
        }
        else
        {
            Texture = device.CreateTexture2D(textureDesc);
        }

        ShaderResourceView = device.CreateShaderResourceView(Texture);

        if (creationSettings.HasMipmaps && pixelData != null)
        {
            using var context = device.ImmediateContext;
            context.GenerateMips(ShaderResourceView);
        }
        
        RSDebugger.Textures.Add(this);
    }

    private static uint CalculateMipLevels(int width, int height)
    {
        int mipLevels = 1;
        int w = width;
        int h = height;
        while (w > 1 || h > 1)
        {
            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
            mipLevels++;
        }
        return (uint)mipLevels;
    }
    public void Dispose()
    {
        if (_disposed) return;

        RSDebugger.Textures.Remove(this);

        ShaderResourceView?.Dispose();
        Texture?.Dispose();

        _disposed = true;
    }

    public void Bind(ID3D11DeviceContext context, uint slot)
    {
        context.PSSetShaderResource(slot, ShaderResourceView);
    }
}