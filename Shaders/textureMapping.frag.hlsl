struct PixelInput
{
    [[vk::location(0)]] float3 color : COLOR;
    [[vk::location(1)]] float2 textureCoordinate : TEXCOORD;
};

float4 main(PixelInput pixelInput) : SV_Target0
{
    float3 inColor = pixelInput.color;
    return float4(pixelInput.textureCoordinate, 0.0f, 1.0f);
}
