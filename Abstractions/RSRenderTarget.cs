using System;
using Vortice.Direct3D11;
using Vortice.DXGI;
using RenderStorm.Display;
using RenderStorm.Other;
using Vortice.Direct3D;
using Vortice.Mathematics;

namespace RenderStorm.Abstractions;

public enum RenderTargetType
{
    ColorAndDepth,  // Standard render target with color and depth
    DepthOnly       // Depth-only render target for shadow mapping
}

public class RSRenderTarget : IProfilerObject, IDisposable
{
    private readonly RSWindow _window;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;

    public uint Width { get; }
    public uint Height { get; }
    public RenderTargetType Type { get; }

    public ID3D11Texture2D ColorTexture { get; private set; }
    public ID3D11RenderTargetView RenderTargetView { get; private set; }
    public ID3D11ShaderResourceView ColorShaderResourceView { get; private set; }

    public ID3D11Texture2D DepthTexture { get; private set; }
    public ID3D11DepthStencilView DepthStencilView { get; private set; }
    public ID3D11ShaderResourceView DepthShaderResourceView { get; private set; }

    public RSRenderTarget(RSWindow window, uint width, uint height, string debugName = "RenderTarget", RenderTargetType type = RenderTargetType.ColorAndDepth)
    {
        DebugName = debugName;
        _window = window;
        _device = window.D3dDeviceContainer.Device;
        _context = window.D3dDeviceContainer.Context;

        Width = width;
        Height = height;
        Type = type;

        CreateRenderTargetResources();
        RSDebugger.RenderTargets.Add(this);
    }

    private void CreateRenderTargetResources()
    {
        // Create depth texture and view for both types of render targets
        var depthDesc = new Texture2DDescription
        {
            Width = Width,
            Height = Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R32_Typeless, // Use higher precision for shadow maps
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource
        };
        DepthTexture = _device.CreateTexture2D(depthDesc);

        var dsvDesc = new DepthStencilViewDescription
        {
            Format = Format.D32_Float, // Higher precision depth format
            ViewDimension = DepthStencilViewDimension.Texture2D
        };
        DepthStencilView = _device.CreateDepthStencilView(DepthTexture, dsvDesc);

        var depthSrvDesc = new ShaderResourceViewDescription
        {
            Format = Format.R32_Float, // Access as single-channel float
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = { MipLevels = 1 }
        };
        DepthShaderResourceView = _device.CreateShaderResourceView(DepthTexture, depthSrvDesc);

        // Only create color resources for standard render targets
        if (Type == RenderTargetType.ColorAndDepth)
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
        }
    }

    public void Begin(Color4? clearColor = null)
    {
        // Set viewport for both types
        _context.RSSetViewport(0, 0, Width, Height);

        if (Type == RenderTargetType.ColorAndDepth)
        {
            // Set both color and depth targets
            _context.OMSetRenderTargets(RenderTargetView, DepthStencilView);

            // Clear both buffers
            _context.ClearRenderTargetView(RenderTargetView, clearColor ?? new Color4(0f, 0f, 0f, 1f));
            _context.ClearDepthStencilView(DepthStencilView,
                DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil,
                1.0f, 0);
        }
        else // DepthOnly
        {
            ID3D11RenderTargetView[] nullRenderTargets = new ID3D11RenderTargetView[1] { null };
            _context.OMSetRenderTargets(nullRenderTargets, DepthStencilView);

            _context.ClearDepthStencilView(DepthStencilView,
                DepthStencilClearFlags.Depth,
                1.0f, 0);
        }
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