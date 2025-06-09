using System;
using System.Drawing;
using System.Numerics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using TracyWrapper;

namespace RenderStorm.Display;

[Obsolete("This class is messy and usage is not recommended unless necessary", false)]
public class D3D11DeviceContainer : IDisposable
{
    private ID3D11Device _device;
    private ID3D11DeviceContext _context;
    private IDXGISwapChain _swapChain;
    private ID3D11RenderTargetView _renderTargetView;
    private ID3D11Texture2D _depthStencilTexture;
    private ID3D11DepthStencilView _depthStencilView;

    public ID3D11RasterizerState RasterizerState;
    public ID3D11DepthStencilState DepthStencilState;
    public ID3D11BlendState BlendState;
    private Rectangle _scissorRect = Rectangle.Empty;
    
    private IntPtr _windowHandle;
    private Viewport _viewport;
    
    public ID3D11Device Device => _device;
    public ID3D11DeviceContext Context => _context;
    public bool VSync = true;

    public static D3D11DeviceContainer SharedState;

    public void SetScissorRect(Rectangle rect)
    {
        Context.RSSetScissorRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        _scissorRect = rect;
    }
    
    public Rectangle GetScissorRect()
    {
        return _scissorRect;
    }

    public D3D11DeviceContainer(IntPtr windowHandle, uint width, uint height)
    {
        SharedState = this;
        _windowHandle = windowHandle;
        InitializeDeviceAndSwapChain(width, height);
        CreateResources(width, height);
        _viewport = new Viewport(0, 0, width, height);
    }

    public void Resize(uint width, uint height)
    {
        _renderTargetView?.Dispose();
        _depthStencilView?.Dispose();
        _depthStencilTexture?.Dispose();
        
        _swapChain.ResizeBuffers(
            0,
            width,
            height,
            Format.Unknown,
            SwapChainFlags.None);
        
        CreateResources(width, height);
        _viewport = new Viewport(0, 0, width, height);
    }

    private void  InitializeDeviceAndSwapChain(uint width, uint height)
    {
        var swapChainDesc = new SwapChainDescription
        {
            BufferCount = 2,
            BufferDescription = new ModeDescription
            {
                Width = width,
                Height = height,
                Format = Format.R8G8B8A8_UNorm,
                RefreshRate = new Rational(60, 1)
            },
            BufferUsage = Usage.RenderTargetOutput,
            OutputWindow = _windowHandle,
            SampleDescription = new SampleDescription(1, 0),
            Windowed = true,
            SwapEffect = SwapEffect.FlipDiscard,
            Flags = SwapChainFlags.None
        };

        D3D11.D3D11CreateDeviceAndSwapChain(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.Debug,
            new[] { FeatureLevel.Level_11_0 },
            swapChainDesc,
            out _swapChain,
            out _device,
            out _,
            out _context
        );
    }

    private void CreateResources(uint width, uint height)
    {
        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device.CreateRenderTargetView(backBuffer);
        
        _depthStencilTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D24_UNorm_S8_UInt,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });
        _depthStencilView = _device.CreateDepthStencilView(_depthStencilTexture);
    }
    
    public FeatureLevel GetFeatureLevel() => _device.FeatureLevel;

    public string GetGroupedInfo()
    {
        using var dxgiDevice = Device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        var desc = adapter.Description;

        string gpuName = desc.Description.TrimEnd('\0');
        uint vendorId = desc.VendorId;
        uint deviceId = desc.DeviceId;
        return $"{gpuName} [VendorId: {vendorId}, DeviceId: {deviceId}]";
    }

    public void InitializeRenderStates()
    {
        using (new TracyWrapper.ProfileScope("Initialize Render States", ZoneC.DARK_SLATE_BLUE))
        {
            RasterizerState = D3D11State.CreateRasterizerState(_device);
            DepthStencilState = D3D11State.CreateDepthStencilState(_device);
            BlendState = D3D11State.CreateBlendState(_device);
        }
    }

    public void ApplyRenderStates()
    {
        using (new TracyWrapper.ProfileScope("Apply Render States", ZoneC.DARK_SLATE_GRAY))
        {
            D3D11State.ApplyStateToContext(Context, RasterizerState, DepthStencilState, BlendState);
        }
    }

    public void SetRenderTargets()
    {
        using (new TracyWrapper.ProfileScope("Set Render Targets", ZoneC.DARK_ORANGE))
        {
            _context.RSSetViewport(_viewport);
            _context.OMSetRenderTargets(_renderTargetView, _depthStencilView);
        }
    }

    public void Clear(float r, float g, float b, float a)
    {
        using (new TracyWrapper.ProfileScope("Clear Render Target", ZoneC.DARK_SLATE_GRAY))
        {
            _context.ClearRenderTargetView(_renderTargetView, new Color4(r, g, b, a));
            _context.ClearDepthStencilView(_depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
        }
    }

    public void Present()
    {
        using (new TracyWrapper.ProfileScope("Present", ZoneC.DARK_ORANGE))
        {
            _swapChain.Present(VSync ? (uint)1 : 0, PresentFlags.None);
        }
    }

    public void Dispose()
    {
        using (new TracyWrapper.ProfileScope("D3D11DeviceContainer Dispose", ZoneC.DARK_SLATE_BLUE))
        {
            _renderTargetView?.Dispose();
            _depthStencilView?.Dispose();
            _depthStencilTexture?.Dispose();
            _swapChain?.Dispose();
            _device?.Dispose();
            _context?.Dispose();
            RasterizerState?.Dispose();
            DepthStencilState?.Dispose();
            BlendState?.Dispose();
        }
    }
}