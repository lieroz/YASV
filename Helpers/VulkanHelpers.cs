using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;

namespace YASV.Helpers;

public static class VulkanHelpers
{
    public static Vk GetApi()
    {
        MultiNativeContext? multiNativeContext = null;
        if (OperatingSystem.IsWindows())
        {
            multiNativeContext = new MultiNativeContext(Vk.CreateDefaultContext(["./Libraries/Native/Windows/Vulkan/vulkan-1.dll"]), null);
        }
        else
        {
            throw new PlatformNotSupportedException($"Unsupporeted platform: {Environment.OSVersion.Platform}");
        }

        var ret = new Vk(multiNativeContext);
        multiNativeContext.Contexts[1] = new LamdaNativeContext(delegate (string x)
        {
            if (x.EndsWith("ProcAddr"))
            {
                return 0;
            }

            nint num = 0;
            num = (nint)ret.GetDeviceProcAddr(ret.CurrentDevice.GetValueOrDefault(), x);
            return (num != 0) ? num : ((nint)ret.GetInstanceProcAddr(ret.CurrentInstance.GetValueOrDefault(), x));
        });
        return ret;
    }
}
