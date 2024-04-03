using Silk.NET.Vulkan;

namespace YASV.VulkanBackend;

public class VulkanDevice
{
    public unsafe VulkanDevice()
    {
        InstanceCreateInfo instanceCreateInfo;
        Instance instance;
        Vk.GetApi().CreateInstance(&instanceCreateInfo, null, &instance);
    }
}