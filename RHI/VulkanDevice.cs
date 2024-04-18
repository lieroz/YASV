namespace YASV.RHI;

using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;

public class VulkanException(string? message) : Exception(message)
{
}

public class VulkanDevice : RenderingDevice
{
#if DEBUG
    private static readonly string[] ValidationLayers = [
        "VK_LAYER_KHRONOS_validation",
    ];
#endif

    private readonly Vk vk = Vk.GetApi();
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
#if DEBUG
        if (!this.CheckValidationLayersSupport())
        {
            throw new VulkanException("Validation layers were requested, but none available found.");
        }
#endif

        uint count = 0;
        if (sdlApi.VulkanGetInstanceExtensions(null, &count, (byte**)null) == SdlBool.False)
        {
            throw new VulkanException($"Couldn't determine required extensions count: {sdlApi.GetErrorS()}.");
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

        var result = this.vk.CreateInstance(instanceCreateInfo, null, out this.instance);
        if (result != Result.Success)
        {
            throw new VulkanException($"Couldn't create Vulkan instance: {result}");
        }
    }

#if DEBUG
    private unsafe bool CheckValidationLayersSupport()
    {
        uint layerCount = 0;
        var result = this.vk.EnumerateInstanceLayerProperties(&layerCount, null);
        if (result != Result.Success)
        {
            throw new VulkanException($"Couldn't determine instance layer properties count: {result}");
        }

        var availableLayersProperties = new LayerProperties[layerCount];
        fixed (LayerProperties* layerPropertiesPtr = availableLayersProperties)
        {
            result = this.vk.EnumerateInstanceLayerProperties(&layerCount, availableLayersProperties);
            if (result != Result.Success)
            {
                throw new VulkanException($"Couldn't enumerate instance layer properties: {result}");
            }
        }

        var availableLayersNames = availableLayersProperties.Select(layer => SilkMarshal.PtrToString((nint)layer.LayerName)).ToHashSet();
        return ValidationLayers.All(availableLayersNames.Contains);
    }
#endif

    private unsafe void DestroyInstance()
    {
        this.vk.DestroyInstance(this.instance, null);
    }
}
