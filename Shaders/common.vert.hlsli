struct Constants
{
    float4x4 modelMatrix;
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
};

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
