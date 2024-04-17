namespace YASV.RHI;

using System;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.SDL;
using Silk.NET.Vulkan;

public class VulkanException(string? message) : Exception(message)
{
}

public class VulkanDevice : RenderingDevice
{
    private Instance instance;

    public override unsafe void Create(Sdl sdlApi)
    {
        this.CreateInstance(sdlApi);
    }

    public override void Destroy()
    {
        this.DestroyInstance();
    }

    private unsafe void CreateInstance(Sdl sdlApi)
    {
        uint count = 0;
        if (sdlApi.VulkanGetInstanceExtensions(null, &count, (byte**)null) == SdlBool.False)
        {
            throw new VulkanException($"Couldn't determine required extensions count: {sdlApi.GetErrorS()}");
        }

        var applicationName = (byte*)Marshal.StringToBSTR("YASV");
        var engineName = (byte*)Marshal.StringToBSTR("No Engine");
        var requiredExtensions = (byte**)Marshal.AllocHGlobal((nint)(sizeof(byte*) * count));

        using var defer = Disposable.Create(() =>
        {
            Marshal.FreeBSTR((nint)applicationName);
            Marshal.FreeBSTR((nint)engineName);
            Marshal.FreeHGlobal((nint)requiredExtensions);
        });

        if (sdlApi.VulkanGetInstanceExtensions(null, &count, requiredExtensions) == SdlBool.False)
        {
            throw new VulkanException($"Couldn't get required extensions: {sdlApi.GetErrorS()}");
        }

        var version = new Version32(0, 1, 0);

        ApplicationInfo applicationInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = applicationName,
            ApplicationVersion = version,
            PEngineName = engineName,
            EngineVersion = version,
            ApiVersion = Vk.Version13,
        };

        InstanceCreateInfo instanceCreateInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &applicationInfo,
            EnabledExtensionCount = count,
            PpEnabledExtensionNames = requiredExtensions,
            EnabledLayerCount = 0,
        };

        var result = Vk.GetApi().CreateInstance(instanceCreateInfo, null, out this.instance);
        if (result != Result.Success)
        {
            throw new VulkanException($"Couldn't create Vulkan instance: {result}");
        }
    }

    private unsafe void DestroyInstance()
    {
        Vk.GetApi().DestroyInstance(this.instance, null);
    }
}
