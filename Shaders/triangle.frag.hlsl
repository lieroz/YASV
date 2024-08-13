#include "common.frag.hlsli"

float4 main(PixelInput pixelInput) : SV_Target0
{
    float3 inColor = pixelInput.color;
    return float4(inColor, 1.0f);
}
