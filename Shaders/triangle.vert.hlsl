static const float2 positions[3] = {
    { 0.0, -0.5 },
    { 0.5, 0.5 },
    { -0.5, 0.5 }
};

static const float3 colors[3] = {
    { 1.0, 0.0, 0.0 },
    { 0.0, 1.0, 0.0 },
    { 0.0, 0.0, 1.0 }
};

struct VertexOutput
{
    [[vk::location(0)]] float3 color : COLOR;
    float4 position : SV_Position;
};

VertexOutput main(uint VertexIndex : SV_VertexID)
{
    float3 inColor = colors[VertexIndex];
    float2 inPos = positions[VertexIndex];

    VertexOutput output;
    output.position = float4(inPos, 0.0, 1.0);
    output.color = inColor;
    return output;
}
