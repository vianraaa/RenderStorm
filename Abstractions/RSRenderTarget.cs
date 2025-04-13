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

    public RSRenderTarget(RSWindow window, uint width, uint height, string debugName = "RenderTarget")
    {
        _window = window;
        _device = window.D3dDeviceContainer.Device;
        _context = window.D3dDeviceContainer.Context;

        Width = width;
        Height = height;

        CreateRenderTargetResources();
        RSDebugger.RenderTargets.Add(this);
    }

    private void CreateRenderTargetResources()
    {
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
    }

    public void Begin()
    {
        _context.OMSetRenderTargets(RenderTargetView, DepthStencilView);
        _context.RSSetViewport(0, 0, Width, Height);
        
        _context.ClearRenderTargetView(RenderTargetView, new Vortice.Mathematics.Color4(0f, 0f, 0f, 1f));
        _context.ClearDepthStencilView(DepthStencilView, 
            DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 
            1.0f, 0);
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
        ColorShaderResourceView?.Dispose();
        RenderTargetView?.Dispose();
        ColorTexture?.Dispose();

        RSDebugger.RenderTargets.Remove(this);
    }
}