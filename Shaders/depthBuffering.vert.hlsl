struct Constants
{
    float4x4 modelMatrix;
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
};

ConstantBuffer<Constants> constants : register(b0);

struct VertexInput
{
    [[vk::location(0)]] float3 position : POSITION0;
    [[vk::location(1)]] float3 color : COLOR0;
    [[vk::location(2)]] float2 textureCoordinate : TEXCOORD0;
};

struct VertexOutput
{
    [[vk::location(0)]] float3 color : COLOR;
    [[vk::location(1)]] float2 textureCoordinate : TEXCOORD;
    float4 position : SV_Position;
};

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    output.position = mul(constants.projectionMatrix, mul(constants.viewMatrix, mul(constants.modelMatrix, float4(input.position, 1.0))));
    output.color = input.color;
    output.textureCoordinate = input.textureCoordinate;
    return output;
}
