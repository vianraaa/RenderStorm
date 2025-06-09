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
    public uint Width { get; }
    public uint Height { get; }
    public RenderTargetType Type { get; }

    public ID3D11Texture2D ColorTexture { get; private set; }
    public ID3D11RenderTargetView RenderTargetView { get; private set; }
    public ID3D11ShaderResourceView ColorShaderResourceView { get; private set; }

    public ID3D11Texture2D DepthTexture { get; private set; }
    public ID3D11DepthStencilView DepthStencilView { get; private set; }
    public ID3D11ShaderResourceView DepthShaderResourceView { get; private set; }

    public RSRenderTarget(uint width, uint height, string debugName = "RenderTarget", RenderTargetType type = RenderTargetType.ColorAndDepth)
    {
        DebugName = debugName;

        Width = width;
        Height = height;
        Type = type;

        CreateRenderTargetResources();
        RSDebugger.RenderTargets.Add(this);
        Begin();
        End();
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
        DepthTexture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(depthDesc);

        var dsvDesc = new DepthStencilViewDescription
        {
            Format = Format.D32_Float, // Higher precision depth format
            ViewDimension = DepthStencilViewDimension.Texture2D
        };
        DepthStencilView = D3D11DeviceContainer.SharedState.Device.CreateDepthStencilView(DepthTexture, dsvDesc);

        var depthSrvDesc = new ShaderResourceViewDescription
        {
            Format = Format.R32_Float, // Access as single-channel float
            ViewDimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = { MipLevels = 1 }
        };
        DepthShaderResourceView = D3D11DeviceContainer.SharedState.Device.CreateShaderResourceView(DepthTexture, depthSrvDesc);

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
            ColorTexture = D3D11DeviceContainer.SharedState.Device.CreateTexture2D(colorDesc);
            RenderTargetView = D3D11DeviceContainer.SharedState.Device.CreateRenderTargetView(ColorTexture);
            ColorShaderResourceView = D3D11DeviceContainer.SharedState.Device.CreateShaderResourceView(ColorTexture);
        }
    }

    public void Begin(Color4? clearColor = null)
    {
        // Set viewport for both types
        D3D11DeviceContainer.SharedState.Context.RSSetViewport(0, 0, Width, Height);

        if (Type == RenderTargetType.ColorAndDepth)
        {
            D3D11DeviceContainer.SharedState.Context.OMSetRenderTargets(RenderTargetView, DepthStencilView);
            D3D11DeviceContainer.SharedState.Context.ClearRenderTargetView(RenderTargetView, clearColor ?? new Color4(0f, 0f, 0f, 1f));
            D3D11DeviceContainer.SharedState.Context.ClearDepthStencilView(DepthStencilView,
                DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil,
                1.0f, 0);
        }
        else // DepthOnly
        {
            ID3D11RenderTargetView[] nullRenderTargets = new ID3D11RenderTargetView[1] { null };
            D3D11DeviceContainer.SharedState.Context.OMSetRenderTargets(nullRenderTargets, DepthStencilView);

            D3D11DeviceContainer.SharedState.Context.ClearDepthStencilView(DepthStencilView,
                DepthStencilClearFlags.Depth,
                1.0f, 0);
        }
    }

    public void End()
    {
        D3D11DeviceContainer.SharedState.SetRenderTargets();
        D3D11DeviceContainer.SharedState.ApplyRenderStates();
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