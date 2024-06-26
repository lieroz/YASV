using Silk.NET.Vulkan;

namespace YASV.RHI;

internal class VulkanCommandBufferWrapper(CommandBuffer commandBuffer) : ICommandBuffer
{
    public CommandBuffer _commandBuffer = commandBuffer;
}

internal static class CommandBufferVulkanExtensions
{
    internal static CommandBuffer ToVulkanCommandBuffer(this ICommandBuffer commandBuffer)
    {
        return ((VulkanCommandBufferWrapper)commandBuffer)._commandBuffer;
    }
}
