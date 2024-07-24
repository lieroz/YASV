struct Constants
{
    float4x4 modelMatrix;
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
};

ConstantBuffer<Constants> constants : register(b0);

struct VertexInput
{
    [[vk::location(0)]] float2 position : POSITION0;
    [[vk::location(1)]] float3 color : COLOR0;
};

struct VertexOutput
{
    [[vk::location(0)]] float3 color : COLOR;
    float4 position : SV_Position;
};

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    output.position = mul(constants.projectionMatrix, (constants.viewMatrix, (constants.modelMatrix, (input.position, 0.0, 1.0))));
    output.color = input.color;
    return output;
}
