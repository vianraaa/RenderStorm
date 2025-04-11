using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RenderStorm;

public static class D3D11State
{
    public static bool DepthTestEnabled { get; set; } = true;
    public static bool CullFaceEnabled { get; set; } = true;
    public static bool InvertCulling { get; set; } = false;
    public static bool AlphaBlendingEnabled { get; set; } = true;
    public static bool DepthWriteEnabled { get; set; } = true;

    
    public static RasterizerDescription RasterizerDesc => new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullFaceEnabled ? CullMode.Back : CullMode.None,
        FrontCounterClockwise = InvertCulling,
        DepthClipEnable = true,
        MultisampleEnable = false,
        AntialiasedLineEnable = false,
        DepthBiasClamp = 0.0f,
        DepthBias = 0,
        SlopeScaledDepthBias = 0.0f,
    };
    public static DepthStencilDescription DepthStencilDesc => new()
    {
        DepthEnable = DepthTestEnabled,
        DepthWriteMask = DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
        DepthFunc = ComparisonFunction.Less,
        StencilEnable = false
    };
    public static BlendDescription BlendDesc => new()
    {
        AlphaToCoverageEnable = false,
        IndependentBlendEnable = false,
        RenderTarget = new BlendDescription.RenderTarget__FixedBuffer
        {
            e0 = new RenderTargetBlendDescription
            {
                BlendEnable = AlphaBlendingEnabled,
                SourceBlend = Blend.SourceAlpha,
                DestinationBlend = Blend.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = Blend.One,
                DestinationBlendAlpha = Blend.Zero,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All
            }
        }
    };
    public static ID3D11RasterizerState CreateRasterizerState(ID3D11Device device)
    {
        return device.CreateRasterizerState(RasterizerDesc);
    }
    public static ID3D11DepthStencilState CreateDepthStencilState(ID3D11Device device)
    {
        return device.CreateDepthStencilState(DepthStencilDesc);
    }
    public static ID3D11BlendState CreateBlendState(ID3D11Device device)
    {
        return device.CreateBlendState(BlendDesc);
    }
    public static void ApplyStateToContext(ID3D11DeviceContext context, 
        ID3D11RasterizerState rasterizerState, ID3D11DepthStencilState depthStencilState, ID3D11BlendState blendState)
    {
        // Set the rasterizer state
        context.RSSetState(rasterizerState);

        // Set the depth stencil state
        context.OMSetDepthStencilState(depthStencilState, 1);

        // Set the blend state
        unsafe
        {
            context.OMSetBlendState(blendState, null, 0xFFFFFFFF);
        }
    }
    public static void ApplyDefaultStates(ID3D11DeviceContext context, ID3D11Device device)
    {
        var rasterizerState = CreateRasterizerState(device);
        var depthStencilState = CreateDepthStencilState(device);
        var blendState = CreateBlendState(device);

        ApplyStateToContext(context, rasterizerState, depthStencilState, blendState);
    }
}