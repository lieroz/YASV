namespace YASV.RHI;

internal class VulkanCommandBufferWrapper(Silk.NET.Vulkan.CommandBuffer commandBuffer) : CommandBuffer
{
    public Silk.NET.Vulkan.CommandBuffer CommandBuffer { get; private set; } = commandBuffer;
}

internal static class CommandBufferVulkanExtensions
{
    internal static Silk.NET.Vulkan.CommandBuffer ToVulkanCommandBuffer(this CommandBuffer commandBuffer)
    {
        return ((VulkanCommandBufferWrapper)commandBuffer).CommandBuffer;
    }
}
