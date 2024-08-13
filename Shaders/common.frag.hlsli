struct PixelInput
{
    [[vk::location(0)]] float3 color : COLOR;
    [[vk::location(1)]] float2 textureCoordinate : TEXCOORD;
};
