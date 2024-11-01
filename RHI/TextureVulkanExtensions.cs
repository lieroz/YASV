using SkiaSharp;

namespace YASV.RHI;

internal class VulkanTextureWrapper(Silk.NET.Vulkan.Image image, Silk.NET.Vulkan.DeviceMemory deviceMemory, Silk.NET.Vulkan.ImageView imageView, uint mipLevels) : Texture(mipLevels)
{
    public Silk.NET.Vulkan.Image Image { get; private set; } = image;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; private set; } = deviceMemory;
    public Silk.NET.Vulkan.ImageView ImageView { get; private set; } = imageView;
}

internal class VulkanTextureSamplerWrapper(Silk.NET.Vulkan.Sampler sampler) : TextureSampler
{
    public Silk.NET.Vulkan.Sampler Sampler { get; private set; } = sampler;
}

internal static class TextureVulkanExtensions
{
    internal static VulkanTextureWrapper ToVulkanTexture(this Texture texture)
    {
        return (VulkanTextureWrapper)texture;
    }

    internal static Silk.NET.Vulkan.Sampler ToVulkanTextureSampler(this TextureSampler textureSampler)
    {
        return ((VulkanTextureSamplerWrapper)textureSampler).Sampler;
    }

    internal static Silk.NET.Vulkan.Format ToVulkanFormat(this SKColorType type)
    {
        // TODO: not sure mapping is correct here
        return type switch
        {
            SKColorType.Unknown => Silk.NET.Vulkan.Format.Undefined,
            SKColorType.Alpha8 => Silk.NET.Vulkan.Format.A8UnormKhr,
            SKColorType.Rgb565 => Silk.NET.Vulkan.Format.R5G6B5UnormPack16,
            SKColorType.Argb4444 => Silk.NET.Vulkan.Format.A4R4G4B4UnormPack16,
            SKColorType.Rgba8888 => Silk.NET.Vulkan.Format.R8G8B8A8Srgb,
            SKColorType.Rgb888x => Silk.NET.Vulkan.Format.R8G8B8A8Sscaled,
            SKColorType.Bgra8888 => Silk.NET.Vulkan.Format.B8G8R8A8Srgb,
            SKColorType.Rgba1010102 => Silk.NET.Vulkan.Format.A2R10G10B10UintPack32,
            SKColorType.Rgb101010x => Silk.NET.Vulkan.Format.A2R10G10B10UscaledPack32,
            SKColorType.Gray8 => Silk.NET.Vulkan.Format.R8Unorm,
            SKColorType.RgbaF16 => Silk.NET.Vulkan.Format.R16G16B16A16Sfloat,
            SKColorType.RgbaF16Clamped => Silk.NET.Vulkan.Format.R16G16B16A16Sscaled,
            SKColorType.RgbaF32 => Silk.NET.Vulkan.Format.R32G32B32A32Sfloat,
            SKColorType.Rg88 => Silk.NET.Vulkan.Format.R8G8Srgb,
            SKColorType.AlphaF16 => Silk.NET.Vulkan.Format.R16Sfloat,
            SKColorType.RgF16 => Silk.NET.Vulkan.Format.R16G16Sfloat,
            SKColorType.Alpha16 => Silk.NET.Vulkan.Format.R16Unorm,
            SKColorType.Rg1616 => Silk.NET.Vulkan.Format.R16G16Unorm,
            SKColorType.Rgba16161616 => Silk.NET.Vulkan.Format.R16G16B16A16Unorm,
            SKColorType.Bgra1010102 => Silk.NET.Vulkan.Format.A2B10G10R10UnormPack32,
            SKColorType.Bgr101010x => Silk.NET.Vulkan.Format.A2B10G10R10UscaledPack32,
            _ => throw new NotSupportedException($"Color type '{type}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.ImageLayout ToVulkanImageLayout(this ImageLayout imageLayout)
    {
        return imageLayout switch
        {
            ImageLayout.Undefined => Silk.NET.Vulkan.ImageLayout.Undefined,
            ImageLayout.General => Silk.NET.Vulkan.ImageLayout.General,
            ImageLayout.ColorAttachmentOptimal => Silk.NET.Vulkan.ImageLayout.ColorAttachmentOptimal,
            ImageLayout.DepthStencilAttachmentOptimal => Silk.NET.Vulkan.ImageLayout.DepthStencilAttachmentOptimal,
            ImageLayout.DepthStencilReadOnlyOptimal => Silk.NET.Vulkan.ImageLayout.DepthStencilReadOnlyOptimal,
            ImageLayout.ShaderReadOnlyOptimal => Silk.NET.Vulkan.ImageLayout.ShaderReadOnlyOptimal,
            ImageLayout.TransferSrcOptimal => Silk.NET.Vulkan.ImageLayout.TransferSrcOptimal,
            ImageLayout.TransferDstOptimal => Silk.NET.Vulkan.ImageLayout.TransferDstOptimal,
            ImageLayout.Preinitialized => Silk.NET.Vulkan.ImageLayout.Preinitialized,
            ImageLayout.Present => Silk.NET.Vulkan.ImageLayout.PresentSrcKhr,
            _ => throw new NotSupportedException($"Image layout '{imageLayout}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.Filter ToVulkanFilter(this Filter filter)
    {
        return filter switch
        {
            Filter.Nearest => Silk.NET.Vulkan.Filter.Nearest,
            Filter.Linear => Silk.NET.Vulkan.Filter.Linear,
            _ => throw new NotSupportedException($"Filter '{filter}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.SamplerAddressMode ToVulkanSamplerAddressMode(this SamplerAddressMode mode)
    {
        return mode switch
        {
            SamplerAddressMode.Repeat => Silk.NET.Vulkan.SamplerAddressMode.Repeat,
            SamplerAddressMode.MirroredRepeat => Silk.NET.Vulkan.SamplerAddressMode.MirroredRepeat,
            SamplerAddressMode.ClampToEdge => Silk.NET.Vulkan.SamplerAddressMode.ClampToEdge,
            SamplerAddressMode.ClampToBorder => Silk.NET.Vulkan.SamplerAddressMode.ClampToBorder,
            SamplerAddressMode.MirrorClampToEdge => Silk.NET.Vulkan.SamplerAddressMode.MirrorClampToEdge,
            _ => throw new NotSupportedException($"Sampler address mode '{mode}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.BorderColor ToVulkanBorderColor(this BorderColor color)
    {
        return color switch
        {
            BorderColor.FloatTransparentBlack => Silk.NET.Vulkan.BorderColor.FloatTransparentBlack,
            BorderColor.IntTransparentBlack => Silk.NET.Vulkan.BorderColor.IntTransparentBlack,
            BorderColor.FloatOpaqueBlack => Silk.NET.Vulkan.BorderColor.FloatOpaqueBlack,
            BorderColor.IntOpaqueBlack => Silk.NET.Vulkan.BorderColor.IntOpaqueBlack,
            BorderColor.FloatOpaqueWhite => Silk.NET.Vulkan.BorderColor.FloatOpaqueWhite,
            BorderColor.IntOpaqueWhite => Silk.NET.Vulkan.BorderColor.IntOpaqueWhite,
            _ => throw new NotSupportedException($"Border color '{color}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.SamplerMipmapMode ToVulkanSamplerMipmapMode(this SamplerMipmapMode mode)
    {
        return mode switch
        {
            SamplerMipmapMode.Nearest => Silk.NET.Vulkan.SamplerMipmapMode.Nearest,
            SamplerMipmapMode.Linear => Silk.NET.Vulkan.SamplerMipmapMode.Linear,
            _ => throw new NotSupportedException($"Sampler mipmap mode '{mode}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.SamplerCreateInfo ToVulkanSampler(this TextureSamplerDesc desc, float maxAnisotropy)
    {
        return new()
        {
            SType = Silk.NET.Vulkan.StructureType.SamplerCreateInfo,
            MagFilter = desc.MagFilter.ToVulkanFilter(),
            MinFilter = desc.MinFilter.ToVulkanFilter(),
            AddressModeU = desc.AddressModeU.ToVulkanSamplerAddressMode(),
            AddressModeV = desc.AddressModeV.ToVulkanSamplerAddressMode(),
            AddressModeW = desc.AddressModeW.ToVulkanSamplerAddressMode(),
            AnisotropyEnable = desc.AnisotropyEnable,
            MaxAnisotropy = maxAnisotropy,
            BorderColor = desc.BorderColor.ToVulkanBorderColor(),
            UnnormalizedCoordinates = desc.UnnormalizedCoordinates,
            CompareEnable = desc.CompareEnable,
            CompareOp = desc.CompareOp.ToVulkanCompareOp(),
            MipmapMode = desc.MipmapMode.ToVulkanSamplerMipmapMode(),
            MipLodBias = desc.MipLodBias,
            MinLod = desc.MinLod,
            MaxLod = desc.MaxLod
        };
    }
}
