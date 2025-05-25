using System;
using System.Runtime.InteropServices;
using System.Numerics;
using RenderStorm.Display;
using RenderStorm.Other;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RenderStorm.Abstractions;

/// <summary>
/// Specifies texture filtering options
/// </summary>
public enum TextureFiltering
{
    Linear,
    Nearest,
    Anisotropic
}

/// <summary>
/// Specifies texture address mode
/// </summary>
public enum TextureAddressMode
{
    Wrap,
    Clamp,
    Mirror,
    Border
}

/// <summary>
/// Configuration settings for texture creation
/// </summary>
public readonly struct TextureCreationSettings
{
    public bool HasMipmaps { get; init; }
    public TextureAddressMode AddressMode { get; init; }
    public TextureFiltering Filtering { get; init; }
    public Format Format { get; init; }
    public bool IsDynamic { get; init; }
    public bool EnableUnorderedAccess { get; init; }
    public Vector4 BorderColor { get; init; }
    public int MaxAnisotropy { get; init; }

    public TextureCreationSettings()
    {
        HasMipmaps = true;
        AddressMode = TextureAddressMode.Wrap;
        Filtering = TextureFiltering.Linear;
        Format = Format.R8G8B8A8_UNorm;
        IsDynamic = false;
        EnableUnorderedAccess = false;
        BorderColor = Vector4.Zero;
        MaxAnisotropy = 4;
    }
    
    public static TextureCreationSettings Default => new();
}

/// <summary>
/// Represents a Direct3D11 texture resource
/// </summary>
public sealed class RSTexture : IProfilerObject, IDisposable
{
    private const int BytesPerPixel = 4;
    
    public uint Width { get; }
    public uint Height { get; }
    public uint MipLevels { get; }
    public Format Format { get; }
    public TextureCreationSettings Settings { get; }
    public string DebugName { get; }
    
    public ID3D11Texture2D Texture { get; private set; }
    public ID3D11ShaderResourceView ShaderResourceView { get; private set; }
    public ID3D11SamplerState SamplerState { get; private set; }
    public ID3D11UnorderedAccessView UnorderedAccessView { get; private set; }
    
    public bool Disposed { get; private set; }
    
    /// <summary>
    /// Creates a new texture with optional initial data
    /// </summary>
    /// <param name="device">D3D11 device</param>
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <param name="pixelData">Optional initial pixel data</param>
    /// <param name="settings">Texture creation settings</param>
    /// <param name="debugName">Debug name for the resource</param>
    public RSTexture(ID3D11Device device, uint width, uint height, byte[] pixelData = null, 
                    TextureCreationSettings? settings = null, string debugName = "RSTexture")
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        
        if (width == 0 || height == 0)
            throw new ArgumentException("Texture dimensions must be greater than zero");
            
        Width = width;
        Height = height;
        Settings = settings ?? TextureCreationSettings.Default;
        Format = Settings.Format;
        DebugName = debugName;
        
        MipLevels = Settings.HasMipmaps ? CalculateMipLevels(width, height) : 1;
        
        CreateTexture(device, pixelData);
        CreateViews(device);
        CreateSamplerState(device);
        
        RSDebugger.Textures.Add(this);
    }
    
    /// <summary>
    /// Creates a texture from an existing ID3D11Texture2D
    /// </summary>
    public RSTexture(ID3D11Device device, ID3D11Texture2D existingTexture, 
                    TextureCreationSettings? settings = null, string debugName = "RSTexture")
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        
        if (existingTexture == null)
            throw new ArgumentNullException(nameof(existingTexture));
            
        Texture = existingTexture;
        Settings = settings ?? TextureCreationSettings.Default;
        DebugName = debugName;
        
        var desc = existingTexture.Description;
        Width = desc.Width;
        Height = desc.Height;
        Format = desc.Format;
        MipLevels = desc.MipLevels;
        
        CreateViews(device);
        CreateSamplerState(device);
        
        RSDebugger.Textures.Add(this);
    }
    
    private void CreateTexture(ID3D11Device device, byte[] pixelData)
    {
        var usage = Settings.IsDynamic ? ResourceUsage.Dynamic : ResourceUsage.Default;
        
        var bindFlags = BindFlags.ShaderResource;
        if (Settings.HasMipmaps)
            bindFlags |= BindFlags.RenderTarget;
        if (Settings.EnableUnorderedAccess)
            bindFlags |= BindFlags.UnorderedAccess;
        
        var cpuAccessFlags = Settings.IsDynamic ? CpuAccessFlags.Write : CpuAccessFlags.None;
        var miscFlags = Settings.HasMipmaps ? ResourceOptionFlags.GenerateMips : ResourceOptionFlags.None;
        
        var textureDesc = new Texture2DDescription
        {
            Width = Width,
            Height = Height,
            MipLevels = MipLevels,
            ArraySize = 1,
            Format = Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = usage,
            BindFlags = bindFlags,
            CPUAccessFlags = cpuAccessFlags,
            MiscFlags = miscFlags
        };
        
        if (pixelData != null)
        {
            uint rowPitch = Width * BytesPerPixel;
            
            var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
            try
            {
                var dataBox = new SubresourceData(handle.AddrOfPinnedObject(), rowPitch);
                Texture = device.CreateTexture2D(textureDesc, new[] { dataBox });
            }
            finally
            {
                handle.Free();
            }
        }
        else
        {
            Texture = device.CreateTexture2D(textureDesc);
        }
    }
    
    private void CreateViews(ID3D11Device device)
    {
        var srvDesc = new ShaderResourceViewDescription
        {
            Format = Format,
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DShaderResourceView
            {
                MipLevels = MipLevels,
                MostDetailedMip = 0
            }
        };
        
        ShaderResourceView = device.CreateShaderResourceView(Texture, srvDesc);
        
        if (Settings.EnableUnorderedAccess)
        {
            var uavDesc = new UnorderedAccessViewDescription
            {
                Format = Format,
                ViewDimension = UnorderedAccessViewDimension.Texture2D,
                Texture2D = new Texture2DUnorderedAccessView
                {
                    MipSlice = 0
                }
            };
            
            UnorderedAccessView = device.CreateUnorderedAccessView(Texture, uavDesc);
        }
        
        if (Settings.HasMipmaps && MipLevels > 1)
        {
            using var context = device.ImmediateContext;
            context.GenerateMips(ShaderResourceView);
        }
    }
    

    private void CreateSamplerState(ID3D11Device device)
    {
        Filter filter;
        switch (Settings.Filtering)
        {
            case TextureFiltering.Nearest:
                filter = Settings.HasMipmaps 
                    ? Filter.MinMagMipPoint
                    : Filter.MinMagMipLinear;
                break;
                
            case TextureFiltering.Anisotropic:
                filter = Filter.Anisotropic;
                break;
                
            case TextureFiltering.Linear:
            default:
                filter = Settings.HasMipmaps 
                    ? Filter.MinMagMipLinear
                    : Filter.MinMagLinearMipPoint;
                break;
        }
        
        var addressMode = Settings.AddressMode switch
        {
            TextureAddressMode.Clamp => Vortice.Direct3D11.TextureAddressMode.Clamp,
            TextureAddressMode.Mirror => Vortice.Direct3D11.TextureAddressMode.Mirror,
            TextureAddressMode.Border => Vortice.Direct3D11.TextureAddressMode.Border,
            _ => Vortice.Direct3D11.TextureAddressMode.Wrap
        };
        
        var samplerDesc = new SamplerDescription
        {
            Filter = filter,
            AddressU = addressMode,
            AddressV = addressMode,
            AddressW = addressMode,
            MipLODBias = 0.0f,
            MaxAnisotropy = (uint)Settings.MaxAnisotropy,
            ComparisonFunc = ComparisonFunction.Never,
            BorderColor = new(Settings.BorderColor.X, Settings.BorderColor.Y, Settings.BorderColor.Z, Settings.BorderColor.W),
            MinLOD = 0,
            MaxLOD = float.MaxValue
        };
        
        SamplerState = device.CreateSamplerState(samplerDesc);
    }
    
    private static uint CalculateMipLevels(uint width, uint height)
    {
        return 1 + (uint)Math.Floor(Math.Log(Math.Max(width, height), 2));
    }
    
    /// <summary>
    /// Updates texture data for dynamic textures
    /// </summary>
    public void UpdateData(ID3D11DeviceContext context, byte[] data)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
            
        if (data == null)
            throw new ArgumentNullException(nameof(data));
            
        if (!Settings.IsDynamic)
            throw new InvalidOperationException("Cannot update a non-dynamic texture");
        
        var mappedResource = context.Map(Texture, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            Marshal.Copy(data, 0, mappedResource.DataPointer, data.Length);
        }
        finally
        {
            context.Unmap(Texture, 0);
        }
        
        if (Settings.HasMipmaps && MipLevels > 1)
        {
            context.GenerateMips(ShaderResourceView);
        }
    }
    
    /// <summary>
    /// Binds the texture to the compute shader
    /// </summary>
    public void BindToComputeShader(ID3D11DeviceContext context, uint slot)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
            
        context.CSSetShaderResource(slot, ShaderResourceView);
        context.CSSetSampler(slot, SamplerState);
    }
    
    /// <summary>
    /// Binds the texture UAV to a compute shader
    /// </summary>
    public void BindUAVToComputeShader(ID3D11DeviceContext context, uint slot)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
            
        if (UnorderedAccessView == null)
            throw new InvalidOperationException("This texture does not have a UAV");
            
        context.CSSetUnorderedAccessView(slot, UnorderedAccessView);
    }
    
    /// <summary>
    /// Creates a new texture containing a specified rectangular region of this texture
    /// </summary>
    /// <param name="device">D3D11 device</param>
    /// <param name="context">D3D11 device context</param>
    /// <param name="x">X coordinate of the top-left corner</param>
    /// <param name="y">Y coordinate of the top-left corner</param>
    /// <param name="width">Width of the region</param>
    /// <param name="height">Height of the region</param>
    /// <param name="settings">Optional texture creation settings for the new texture</param>
    /// <param name="debugName">Debug name for the new texture</param>
    /// <returns>A new RSTexture containing the specified region</returns>
    public RSTexture GetSubrect(D3D11DeviceContainer container, 
                              uint x, uint y, uint width, uint height,
                              TextureCreationSettings? settings = null, 
                              string debugName = null)
    {
        ID3D11Device device = container.Device;
        ID3D11DeviceContext context = container.Context;
        if (device == null)
            throw new ArgumentNullException(nameof(device));
            
        if (context == null)
            throw new ArgumentNullException(nameof(context));
            
        // Validate parameters
        if (x + width > Width)
            throw new ArgumentException($"Region width ({width}) at X position {x} exceeds source texture width ({Width})");
            
        if (y + height > Height)
            throw new ArgumentException($"Region height ({height}) at Y position {y} exceeds source texture height ({Height})");
            
        if (width == 0 || height == 0)
            throw new ArgumentException("Subrect dimensions must be greater than zero");
            
        // Use staging texture to read from source
        var stagingDesc = new Texture2DDescription
        {
            Width = Width,
            Height = Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };
        
        using var stagingTexture = device.CreateTexture2D(stagingDesc);
        
        // Copy source texture to staging texture
        context.CopyResource(Texture, stagingTexture);
        
        // Map the staging texture to read pixel data
        var mappedResource = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            // Calculate sizes
            uint srcRowPitch = mappedResource.RowPitch;
            uint newRowPitch = width * BytesPerPixel;
            byte[] subPixelData = new byte[width * height * BytesPerPixel];
            
            // Create pointer to the first pixel of the source region
            IntPtr srcPtr = IntPtr.Add(mappedResource.DataPointer, (int)(y * srcRowPitch + x * BytesPerPixel));
            
            // Extract the region row by row
            for (uint row = 0; row < height; row++)
            {
                // Copy one row of pixels
                Marshal.Copy(
                    IntPtr.Add(srcPtr, (int)(row * srcRowPitch)), 
                    subPixelData, 
                    (int)(row * newRowPitch), 
                    (int)newRowPitch);
            }
            
            // Create the new texture with the extracted data
            return new RSTexture(
                device, 
                width, 
                height, 
                subPixelData,
                settings ?? Settings, // Use original settings by default 
                debugName ?? $"{DebugName}_Subrect");
        }
        finally
        {
            context.Unmap(stagingTexture, 0);
        }
    }
    
    public void Dispose()
    {
        if (Disposed) 
            return;

        RSDebugger.Textures.Remove(this);

        UnorderedAccessView?.Dispose();
        ShaderResourceView?.Dispose();
        SamplerState?.Dispose();
        Texture?.Dispose();

        UnorderedAccessView = null;
        ShaderResourceView = null;
        SamplerState = null;
        Texture = null;

        Disposed = true;
    }
}