namespace YASV.RHI;

internal class VulkanDescriptorSetWrapper(Silk.NET.Vulkan.DescriptorSet descriptorSet) : DescriptorSet
{
    public Silk.NET.Vulkan.DescriptorSet DescriptorSet { get; private set; } = descriptorSet;
}

internal static class DescriptorSetVulkanExtensions
{
    internal static Silk.NET.Vulkan.DescriptorSet ToVulkanDescriptorSet(this DescriptorSet descriptorSet)
    {
        return ((VulkanDescriptorSetWrapper)descriptorSet).DescriptorSet;
    }

    internal static VulkanDescriptorWriter ToVulkanDescriptorWriter(this DescriptorWriter writer)
    {
        return (VulkanDescriptorWriter)writer;
    }
}
