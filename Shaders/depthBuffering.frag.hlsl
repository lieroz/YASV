struct PixelInput
{
    [[vk::location(0)]] float3 color : COLOR;
    [[vk::location(1)]] float2 textureCoordinate : TEXCOORD;
};

Texture2D texture : register(t1);
SamplerState textureSampler: register(s1);

float4 main(PixelInput pixelInput) : SV_Target0
{
    return texture.Sample(textureSampler, pixelInput.textureCoordinate);
    // return float4(pixelInput.color * texture.Sample(textureSampler, pixelInput.textureCoordinate).rgb, 1.0f);
}
