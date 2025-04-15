using System;
using Vortice.Direct3D11;
using Vortice.DXGI;
using RenderStorm.Display;
using RenderStorm.Other;
using Vortice.Direct3D;

namespace RenderStorm.Abstractions;

public class RSRenderTarget : IProfilerObject, IDisposable
{
    private readonly RSWindow _window;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;

    public uint Width { get; }
    public uint Height { get; }

    public ID3D11Texture2D ColorTexture { get; private set; }
    public ID3D11RenderTargetView RenderTargetView { get; private set; }
    public ID3D11ShaderResourceView ColorShaderResourceView { get; private set; }

    public ID3D11Texture2D DepthTexture { get; private set; }
    public ID3D11DepthStencilView DepthStencilView { get; private set; }
    public ID3D11ShaderResourceView DepthShaderResourceView { get; private set; }
    public ID3D11SamplerState SamplerState { get; private set; }
    public bool DepthOnly { get; }

    public RSRenderTarget(RSWindow window, uint width, uint height, bool depthOnly = false, string debugName = "RenderTarget")
    {
        DebugName = debugName;
        _window = window;
        _device = window.D3dDeviceContainer.Device;
        _context = window.D3dDeviceContainer.Context;

        Width = width;
        Height = height;

        DepthOnly = depthOnly;

        CreateRenderTargetResources();
        RSDebugger.RenderTargets.Add(this);
    }

    private void CreateRenderTargetResources()
{
    if (!DepthOnly)
    {
        // Color Render Target Resources (if not depth-only)
        var colorDesc = new Texture2DDescription
        {
            Width = Width,
            Height = Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
        };
        ColorTexture = _device.CreateTexture2D(colorDesc);
        RenderTargetView = _device.CreateRenderTargetView(ColorTexture);
        ColorShaderResourceView = _device.CreateShaderResourceView(ColorTexture);
    }

    // Depth Render Target Resources (always present)
    var depthDesc = new Texture2DDescription
    {
        Width = Width,
        Height = Height,
        MipLevels = 1,
        ArraySize = 1,
        Format = Format.R24G8_Typeless,
        SampleDescription = new SampleDescription(1, 0),
        Usage = ResourceUsage.Default,
        BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource
    };
    DepthTexture = _device.CreateTexture2D(depthDesc);

    var dsvDesc = new DepthStencilViewDescription
    {
        Format = Format.D24_UNorm_S8_UInt,
        ViewDimension = DepthStencilViewDimension.Texture2D
    };
    DepthStencilView = _device.CreateDepthStencilView(DepthTexture, dsvDesc);

    var srvDesc = new ShaderResourceViewDescription
    {
        Format = Format.R24_UNorm_X8_Typeless,
        ViewDimension = ShaderResourceViewDimension.Texture2D,
        Texture2D = { MipLevels = 1 }
    };
    DepthShaderResourceView = _device.CreateShaderResourceView(DepthTexture, srvDesc);

    // Create Sampler State for texture sampling
    var samplerDesc = new SamplerDescription
    {
        Filter = Filter.MinMagMipLinear, // Linear filtering
        AddressU = Vortice.Direct3D11.TextureAddressMode.Wrap, // Wrap address mode
        AddressV = Vortice.Direct3D11.TextureAddressMode.Wrap,
        AddressW = Vortice.Direct3D11.TextureAddressMode.Wrap,
        MipLODBias = 0.0f,
        MaxAnisotropy = 1,
        ComparisonFunc = ComparisonFunction.Never, // No comparison
        BorderColor = new Vortice.Mathematics.Color4(0, 0, 0, 0), // Border color
        MinLOD = 0,
        MaxLOD = float.MaxValue
    };
    SamplerState = _device.CreateSamplerState(samplerDesc);
}


    public void Begin()
    {
        if (!DepthOnly)
        {
            _context.OMSetRenderTargets(RenderTargetView, DepthStencilView);
        }
        else
        {
            _context.OMSetRenderTargets([null], DepthStencilView);
        }

        _context.RSSetViewport(0, 0, Width, Height);
        
        if (!DepthOnly)
        {
            // Clear color target
            _context.ClearRenderTargetView(RenderTargetView, new Vortice.Mathematics.Color4(0f, 0f, 0f, 1f));
        }
        
        // Clear depth buffer
        _context.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
    }

    public void End()
    {
        _window.D3dDeviceContainer.SetRenderTargets();
        _window.D3dDeviceContainer.ApplyRenderStates();
    }

    public void Dispose()
    {
        DepthShaderResourceView?.Dispose();
        DepthStencilView?.Dispose();
        DepthTexture?.Dispose();
        SamplerState?.Dispose();
        if (!DepthOnly)
        {
            ColorShaderResourceView?.Dispose();
            RenderTargetView?.Dispose();
            ColorTexture?.Dispose();
        }

        RSDebugger.RenderTargets.Remove(this);
    }
}
