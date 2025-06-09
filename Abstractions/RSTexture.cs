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
    public uint MipLevels { get; private set; } // Changed to private set
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
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <param name="pixelData">Optional initial pixel data</param>
    /// <param name="settings">Texture creation settings</param>
    /// <param name="debugName">Debug name for the resource</param>
    public RSTexture(uint width, uint height, byte[] pixelData = null, 
                    TextureCreationSettings? settings = null, string debugName = "RSTexture")
    {
        if (width == 0 || height == 0)
            throw new ArgumentException("Texture dimensions must be greater than zero");
            
        Width = width;
        Height = height;
        Settings = settings ?? TextureCreationSettings.Default;
        Format = Settings.Format;
        DebugName = debugName;
        
        CreateTexture(pixelData);
        CreateViews();
        CreateSamplerState();
        
        RSDebugger.Textures.Add(this);
    }
    
    /// <summary>
    /// Creates a texture from an existing ID3D11Texture2D
    /// </summary>
    public RSTexture(ID3D11Texture2D existingTexture, 
                    TextureCreationSettings? settings = null, string debugName = "RSTexture")
    {
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
        
        CreateViews();
        CreateSamplerState();
        
        RSDebugger.Textures.Add(this);
    }
    
    private void CreateTexture(byte[] pixelData)
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
            MipLevels = Settings.HasMipmaps ? 0u : 1u,
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
            // Validate pixel data size
            uint expectedSize = Width * Height * BytesPerPixel;
            if (pixelData.Length != expectedSize)
            {
                throw new ArgumentException($"Pixel data size ({pixelData.Length}) does not match expected size ({expectedSize}) for {Width}x{Height} texture");
            }

            uint rowPitch = Width * BytesPerPixel;
            uint slicePitch = Height * rowPitch;

            var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
            try
            {
                var dataBox = new SubresourceData(handle.AddrOfPinnedObject(), rowPitch, slicePitch);
                
                if (Settings.HasMipmaps)
                {
                    Texture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(textureDesc);
                    
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
                        CPUAccessFlags = CpuAccessFlags.Write,
                        MiscFlags = ResourceOptionFlags.None
                    };
    
                    using var stagingTexture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(stagingDesc, new[] { dataBox });
                    
                    D3D11DeviceContainer.SharedState.Context.CopySubresourceRegion(
                        Texture, 
                        0,
                        0,
                        0, 
                        0,
                        stagingTexture, 0);
                }
                else
                {
                    Texture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(textureDesc, new[] { dataBox });
                }
            }
            finally
            {
                handle.Free();
            }
        }
        else
        {
            Texture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(textureDesc);
        }
        
        var actualDesc = Texture.Description;
        MipLevels = actualDesc.MipLevels;
    }

    private void CreateViews()
    {
        var srvDesc = new ShaderResourceViewDescription
        {
            Format = Format,
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DShaderResourceView
            {
                MipLevels = unchecked((uint)-1), // Use all available mip levels
                MostDetailedMip = 0
            }
        };
        
        ShaderResourceView = D3D11DeviceContainer.SharedState.Device.CreateShaderResourceView(Texture, srvDesc);
        
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
            
            UnorderedAccessView = D3D11DeviceContainer.SharedState.Device.CreateUnorderedAccessView(Texture, uavDesc);
        }
        
        // Generate mips AFTER creating the SRV and ONLY if we have mipmaps enabled and more than 1 mip level
        if (Settings.HasMipmaps && MipLevels > 1)
        {
            // Verify the format supports mip generation
            if (!IsFormatSupportedForMipGeneration(Format))
            {
                throw new InvalidOperationException($"Format {Format} does not support automatic mip generation");
            }
            
            // Make sure we have valid data in mip 0 before generating
            D3D11DeviceContainer.SharedState.Context.GenerateMips(ShaderResourceView);
        }
    }
    
    // Helper method to check format support for mip generation
    private static bool IsFormatSupportedForMipGeneration(Format format)
    {
        // Common formats that support mip generation
        return format switch
        {
            Format.R8G8B8A8_UNorm or
            Format.R8G8B8A8_UNorm_SRgb or
            Format.B8G8R8A8_UNorm or
            Format.B8G8R8A8_UNorm_SRgb or
            Format.R16G16B16A16_Float or
            Format.R32G32B32A32_Float or
            Format.R8_UNorm or
            Format.R16_Float or
            Format.R32_Float => true,
            _ => false
        };
    }

    private void CreateSamplerState()
    {
        Filter filter;
        switch (Settings.Filtering)
        {
            case TextureFiltering.Nearest:
                filter = Settings.HasMipmaps 
                    ? Filter.MinMagMipPoint
                    : Filter.MinMagLinearMipPoint; // Fixed: was MinMagMipLinear
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
            MaxLOD = Settings.HasMipmaps ? float.MaxValue : 0.0f // Limit MaxLOD when no mipmaps
        };
        
        SamplerState = D3D11DeviceContainer.SharedState.Device.CreateSamplerState(samplerDesc);
    }
    
    private static uint CalculateMipLevels(uint width, uint height)
    {
        return 1 + (uint)Math.Floor(Math.Log(Math.Max(width, height), 2));
    }
    
    /// <summary>
    /// Binds the texture to the compute shader
    /// </summary>
    public void BindToComputeShader(uint slot)
    {
        D3D11DeviceContainer.SharedState.Context.CSSetShaderResource(slot, ShaderResourceView);
        D3D11DeviceContainer.SharedState.Context.CSSetSampler(slot, SamplerState);
    }
    
    /// <summary>
    /// Binds the texture UAV to a compute shader
    /// </summary>
    public void BindUAVToComputeShader(uint slot)
    {
        if (UnorderedAccessView == null)
            throw new InvalidOperationException("This texture does not have a UAV");
            
        D3D11DeviceContainer.SharedState.Context.CSSetUnorderedAccessView(slot, UnorderedAccessView);
    }
    
    /// <summary>
    /// Creates a new texture containing a specified rectangular region of this texture
    /// </summary>
    /// <param name="x">X coordinate of the top-left corner</param>
    /// <param name="y">Y coordinate of the top-left corner</param>
    /// <param name="width">Width of the region</param>
    /// <param name="height">Height of the region</param>
    /// <param name="settings">Optional texture creation settings for the new texture</param>
    /// <param name="debugName">Debug name for the new texture</param>
    /// <returns>A new RSTexture containing the specified region</returns>
    public RSTexture GetSubrect(uint x, uint y, uint width, uint height,
                              TextureCreationSettings? settings = null, 
                              string debugName = null)
    {
        ID3D11Device device = D3D11DeviceContainer.SharedState.Device;
        ID3D11DeviceContext context = D3D11DeviceContainer.SharedState.Context;
            
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
        
        context.CopyResource(Texture, stagingTexture);
        
        var mappedResource = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            uint srcRowPitch = mappedResource.RowPitch;
            uint newRowPitch = width * BytesPerPixel;
            byte[] subPixelData = new byte[width * height * BytesPerPixel];
            
            IntPtr srcPtr = IntPtr.Add(mappedResource.DataPointer, (int)(y * srcRowPitch + x * BytesPerPixel));
            
            for (uint row = 0; row < height; row++)
            {
                Marshal.Copy(
                    IntPtr.Add(srcPtr, (int)(row * srcRowPitch)), 
                    subPixelData, 
                    (int)(row * newRowPitch), 
                    (int)newRowPitch);
            }
            
            return new RSTexture(
                width, 
                height, 
                subPixelData,
                settings ?? Settings,
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