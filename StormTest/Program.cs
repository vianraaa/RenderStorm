using System.Numerics;
using RenderStorm;
using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStorm.Types;

namespace StormTest;

struct TestVertex
{
    public Vector3 POSITION;
    public Vector3 COLOR;
}

struct MatrixBufferData
{
    public Matrix4x4 WorldViewProjection;
}
class Program
{
    static RSVertexArray<TestVertex> testArray;
    static RSShader<TestVertex> testShader;
    static void Main(string[] args)
    {
        using RSWindow win = new RSWindow();
        List<RSTexture> textures = new List<RSTexture>();
        win.ViewBegin += () =>
        {
            testShader = new RSShader<TestVertex>(win.D3dDeviceContainer.Device, 
                @"
cbuffer MatrixBuffer : register(b0)
{
    row_major float4x4 worldViewProjection;
}

struct VertexIn
{
    float3 POSITION : POSITION;
    float3 COLOR : COLOR;
};

struct VertexOut
{
    float4 POSITION : SV_POSITION;
    float3 COLOR : COLOR;
};

VertexOut vert(VertexIn input)
{
    VertexOut output;
    
    output.POSITION = mul(float4(input.POSITION, 1.0f), worldViewProjection);
    output.COLOR = input.COLOR;
    
    return output;
}

float4 frag(VertexOut input) : SV_Target
{
    return float4(input.COLOR, 1.0f);
}
");
            testArray = new RSVertexArray<TestVertex>(win.D3dDeviceContainer.Device, 
                [
                    new TestVertex{POSITION = new Vector3(-1, -1, 0), COLOR = new Vector3(1, 0, 0)},
                    new TestVertex{POSITION = new Vector3(0, 1, 0), COLOR = new Vector3(0, 1, 0)},
                    new TestVertex{POSITION = new Vector3(1, -1, 0), COLOR = new Vector3(0, 0, 1)}
            ], 
                [
                    0, 1, 2
            ], testShader);

            for (int i = 0; i < 25; i++)
            {
                /*textures.Add(new RSTexture(win.D3dDeviceContainer.Device, 2, 2, [
                    0xff, 0xff, 0xff, 0xff,
                    0xff, 0xff, 0xff, 0xff,
                    0xff, 0xff, 0xff, 0xff,
                    0xff, 0xff, 0xff, 0xff,
                ], new RSTextureCreationSettings{HasMipmaps = false}));*/
            }
            
            D3D11State.DepthClipEnable = true;
            D3D11State.DepthWriteEnabled = true;
            D3D11State.DepthTestEnabled = true;
            D3D11State.CullFaceEnabled = false;
        };
        win.ViewEnd += () =>
        {
            testArray.Dispose();
            testShader?.Dispose();
            foreach (var tex in textures)
            {
                tex.Dispose();
            }
        };
        double spin = 0.0f;
        win.ViewUpdate += d =>
        {
            spin += d;
            Matrix4x4 drawMatrix = Matrix4x4.CreateRotationY((float)spin) * 
                                   Matrix4x4.CreateTranslation(new Vector3(0, 0, -3)) * 
                                   Matrix4x4.CreatePerspectiveFieldOfView(1.5f, win.GetAspect(), 0.1f, 1024.0f);
            MatrixBufferData data = new MatrixBufferData
            {
                WorldViewProjection = drawMatrix
            };
            testShader.SetUniform(win.D3dDeviceContainer, 0, data);
            testArray.DrawIndexed(win.D3dDeviceContainer);
        };
        win.Run();
    }
}