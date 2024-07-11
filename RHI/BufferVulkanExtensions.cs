using System;

namespace YASV.RHI;

internal class VulkanBufferWrapper(int size, Silk.NET.Vulkan.Buffer buffer, Silk.NET.Vulkan.DeviceMemory deviceMemory) : Buffer(size)
{
    public Silk.NET.Vulkan.Buffer Buffer { get; set; } = buffer;
    public Silk.NET.Vulkan.DeviceMemory DeviceMemory { get; set; } = deviceMemory;
}

internal static class BufferVulkanExtensions
{
    internal static VulkanBufferWrapper ToVulkanBuffer(this Buffer buffer)
    {
        return (VulkanBufferWrapper)buffer;
    }

    internal static Silk.NET.Vulkan.BufferUsageFlags ToVulkanBufferUsageFlags(this BufferUsage usage)
    {
        return usage switch
        {
            BufferUsage.None => Silk.NET.Vulkan.BufferUsageFlags.None,
            BufferUsage.TransferSrc => Silk.NET.Vulkan.BufferUsageFlags.TransferSrcBit,
            BufferUsage.TransferDst => Silk.NET.Vulkan.BufferUsageFlags.TransferDstBit,
            BufferUsage.UniformTexel => Silk.NET.Vulkan.BufferUsageFlags.UniformTexelBufferBit,
            BufferUsage.StorageTexel => Silk.NET.Vulkan.BufferUsageFlags.StorageTexelBufferBit,
            BufferUsage.Uniform => Silk.NET.Vulkan.BufferUsageFlags.UniformBufferBit,
            BufferUsage.Storage => Silk.NET.Vulkan.BufferUsageFlags.StorageBufferBit,
            BufferUsage.Index => Silk.NET.Vulkan.BufferUsageFlags.IndexBufferBit,
            BufferUsage.Vertex => Silk.NET.Vulkan.BufferUsageFlags.VertexBufferBit,
            BufferUsage.Indirect => Silk.NET.Vulkan.BufferUsageFlags.IndirectBufferBit,
            _ => throw new NotSupportedException($"Buffer usage '{usage}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.SharingMode ToVulkanSharingMode(this SharingMode mode)
    {
        return mode switch
        {
            SharingMode.Exclusive => Silk.NET.Vulkan.SharingMode.Exclusive,
            SharingMode.Concurrent => Silk.NET.Vulkan.SharingMode.Concurrent,
            _ => throw new NotSupportedException($"Sharing mode '{mode}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.BufferCreateInfo ToVulkanBuffer(this BufferDesc desc)
    {
        return new()
        {
            SType = Silk.NET.Vulkan.StructureType.BufferCreateInfo,
            Size = (ulong)desc.Size,
            Usage = desc.Usage.ToVulkanBufferUsageFlags(),
            SharingMode = desc.SharingMode.ToVulkanSharingMode()
        };
    }
}
