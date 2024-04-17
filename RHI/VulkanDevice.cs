namespace YASV.RHI;

using Silk.NET.SDL;

public class VulkanDevice : RenderingDevice
{
    public override unsafe void Initialize(Sdl sdlApi)
    {
        // VkHandle instance = new();
        // VkNonDispatchableHandle surface;
        // sdlApi.VulkanCreateSurface((Silk.NET.SDL.Window*)window.Handle.ToPointer(), instance, &surface);

        // InstanceCreateInfo instanceCreateInfo;
        // Instance instance;
        // Vk.GetApi().CreateInstance(&instanceCreateInfo, null, &instance);
    }
}
