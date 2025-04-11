using RenderStorm.Abstractions;
using RenderStorm.Display;
using RenderStorm.Types;

namespace StormTest;

struct TestVertex
{
    public Vec3 POSITION;
    public Vec3 COLOR;
}
class Program
{
    static RSVertexArray<TestVertex> testArray;
    static RSShader<TestVertex> testShader;
    static void Main(string[] args)
    {
        using RSWindow win = new RSWindow();
        win.ViewBegin += () =>
        {
            testShader = new RSShader<TestVertex>(win.D3dDeviceContainer.Device, 
                win.D3dDeviceContainer.Context, 
                @"
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
VertexOut Main(VertexIn input)
{
    VertexOut output;
    
    output.POSITION = float4(input.POSITION, 1.0f);
    output.COLOR = input.COLOR;
    
    return output;
}
", 
                @"
struct VertexOut
{
    float4 POSITION : SV_POSITION;
    float3 COLOR : COLOR;
};

float4 Main(VertexOut input) : SV_Target
{
    return float4(input.COLOR, 1.0f);
}
");
            testArray = new RSVertexArray<TestVertex>(win.D3dDeviceContainer.Device, 
                [
                    new TestVertex{POSITION = new Vec3(-1, -1, 0), COLOR = new Vec3(1, 0, 0)},
                    new TestVertex{POSITION = new Vec3(0, 1, 0), COLOR = new Vec3(0, 1, 0)},
                    new TestVertex{POSITION = new Vec3(1, -1, 0), COLOR = new Vec3(0, 0, 1)}
            ], 
                [
                    0, 1, 2
            ], testShader);
        };
        win.ViewEnd += () =>
        {
            testArray.Dispose();
            testShader?.Dispose();
        };
        win.ViewUpdate += d =>
        {
            testArray.DrawIndexed(win.D3dDeviceContainer.Context);
        };
        win.Run();
    }
}