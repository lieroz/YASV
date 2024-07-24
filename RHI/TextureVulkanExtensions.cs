namespace YASV.RHI;

internal static class TextureVulkanExtensions
{
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
}
