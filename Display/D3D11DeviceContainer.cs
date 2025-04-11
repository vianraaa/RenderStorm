using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RenderStorm.Display;

public class D3D11DeviceContainer: IDisposable
{
    private ID3D11Device _device;
    private ID3D11DeviceContext _context;
    private IDXGISwapChain _swapChain;
    private ID3D11RenderTargetView _renderTargetView;
    private ID3D11DepthStencilView _depthStencilView;
    
    private ID3D11RasterizerState _rasterizerState;
    private ID3D11DepthStencilState _depthStencilState;
    private ID3D11BlendState _blendState;
    
    private IntPtr _windowHandle;
    
    public ID3D11Device Device => _device;
    public ID3D11DeviceContext Context => _context;
    public D3D11DeviceContainer(IntPtr windowHandle, uint width, uint height)
    {
        _windowHandle = windowHandle;
        
        InitializeDeviceAndContext();
        InitializeSwapChain(width, height);
        InitializeRenderTargetAndDepthStencil(width, height);
        InitializeRenderStates();
    }
    private void InitializeDeviceAndContext()
    {

        var creationFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug;

        _device = D3D11.D3D11CreateDevice(
            DriverType.Hardware,
            creationFlags,
            FeatureLevel.Level_11_0
        );
        _context = _device.ImmediateContext;
    }
    private void InitializeSwapChain(uint width, uint height)
    {
        var swapChainDesc = new SwapChainDescription
        {
            BufferCount = 1,
            BufferDescription = new ModeDescription
            {
                Width = width,
                Height = height,
                Format = Format.R8G8B8A8_UNorm,
                Scaling = ModeScaling.Unspecified,
                ScanlineOrdering = ModeScanlineOrder.Unspecified,
                RefreshRate = new Rational(60, 1),
            },
            BufferUsage = Usage.RenderTargetOutput,
            OutputWindow = _windowHandle,
            SampleDescription = new SampleDescription(1, 0),
            Windowed = true,
            Flags = SwapChainFlags.None,
        };

        var dxgiFactory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        _swapChain = dxgiFactory.CreateSwapChain(_device, swapChainDesc);
    }
    private void InitializeRenderTargetAndDepthStencil(uint width, uint height)
    {
        var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device.CreateRenderTargetView(backBuffer);
        
        var depthStencilTexture = _device.CreateTexture2D(new Texture2DDescription
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

        _depthStencilView = _device.CreateDepthStencilView(depthStencilTexture);
    }
    private void InitializeRenderStates()
    {
        _rasterizerState = D3D11State.CreateRasterizerState(_device);
        _depthStencilState = D3D11State.CreateDepthStencilState(_device);
        _blendState = D3D11State.CreateBlendState(_device);
    }
    public void SetRenderTargets()
    {
        _context.OMSetRenderTargets(_renderTargetView, _depthStencilView);
    }
    public void Clear(float r, float g, float b, float a)
    {
        _context.ClearRenderTargetView(_renderTargetView, new Color4(r, g, b, a));
        _context.ClearDepthStencilView(_depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
    }
    public void Present()
    {
        _swapChain.Present(1, PresentFlags.None);
    }
    public void ApplyDefaultStates()
    {
        D3D11State.ApplyDefaultStates(_context, _device);
    }

    public void Dispose()
    {
        _renderTargetView?.Dispose();
        _depthStencilView?.Dispose();
        _swapChain?.Dispose();
        _device?.Dispose();
        _context?.Dispose();
        _rasterizerState?.Dispose();
        _depthStencilState?.Dispose();
        _blendState?.Dispose();
    }
}