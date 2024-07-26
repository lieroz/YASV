namespace YASV.RHI;

internal class VulkanVertexBufferWrapper(int size, Silk.NET.Vulkan.Buffer buffer, Silk.NET.Vulkan.DeviceMemory deviceMemory) : VertexBuffer(size)
{
    public Silk.NET.Vulkan.Buffer Buffer { get; private set; } = buffer;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; private set; } = deviceMemory;
}

internal class VulkanIndexBufferWrapper(int size, Silk.NET.Vulkan.Buffer buffer, Silk.NET.Vulkan.DeviceMemory deviceMemory) : IndexBuffer(size)
{
    public Silk.NET.Vulkan.Buffer Buffer { get; private set; } = buffer;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; private set; } = deviceMemory;
}

internal class VulkanConstantBufferWrapper(int size, Silk.NET.Vulkan.Buffer buffer, Silk.NET.Vulkan.DeviceMemory deviceMemory) : ConstantBuffer(size)
{
    public Silk.NET.Vulkan.Buffer Buffer { get; private set; } = buffer;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; private set; } = deviceMemory;
}

internal class VulkanStagingBufferWrapper(int size, Silk.NET.Vulkan.Buffer buffer, Silk.NET.Vulkan.DeviceMemory deviceMemory) : StagingBuffer(size)
{
    public Silk.NET.Vulkan.Buffer Buffer { get; private set; } = buffer;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; private set; } = deviceMemory;
}

internal static class BufferVulkanExtensions
{
    internal static VulkanVertexBufferWrapper ToVulkanVertexBuffer(this VertexBuffer buffer)
    {
        return (VulkanVertexBufferWrapper)buffer;
    }

    internal static VulkanIndexBufferWrapper ToVulkanIndexBuffer(this IndexBuffer buffer)
    {
        return (VulkanIndexBufferWrapper)buffer;
    }

    internal static VulkanConstantBufferWrapper ToVulkanConstantBuffer(this ConstantBuffer buffer)
    {
        return (VulkanConstantBufferWrapper)buffer;
    }

    internal static VulkanStagingBufferWrapper ToVulkanStagingBuffer(this StagingBuffer buffer)
    {
        return (VulkanStagingBufferWrapper)buffer;
    }

    internal static Silk.NET.Vulkan.IndexType ToVulkanIndexType(this IndexType indexType)
    {
        return indexType switch
        {
            IndexType.Uint16 => Silk.NET.Vulkan.IndexType.Uint16,
            IndexType.Uint32 => Silk.NET.Vulkan.IndexType.Uint32,
            _ => throw new NotSupportedException($"Index type '{indexType}' is not supported.")
        };
    }
}
