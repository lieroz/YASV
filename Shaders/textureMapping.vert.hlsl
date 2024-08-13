#include "common.vert.hlsli"

ConstantBuffer<Constants> constants : register(b0);

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    output.position = mul(constants.projectionMatrix, mul(constants.viewMatrix, mul(constants.modelMatrix, float4(input.position, 1.0f))));
    output.color = input.color;
    output.textureCoordinate = input.textureCoordinate;
    return output;
}
