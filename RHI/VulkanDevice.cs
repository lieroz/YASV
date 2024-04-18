namespace YASV.RHI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

internal sealed class VulkanException(string? message = null) : Exception(message)
{
    public static void ThrowsIf(bool throws, string? message = null)
    {
        if (throws)
        {
            throw new VulkanException(message);
        }
    }
}

public class VulkanDevice : RenderingDevice
{
#if DEBUG
    private static readonly string[] ValidationLayers = [
        "VK_LAYER_KHRONOS_profiles",
        "VK_LAYER_KHRONOS_validation",
    ];
#endif

    private readonly Vk vk = Vk.GetApi();
    private Instance instance;
#if DEBUG
    private ExtDebugUtils? extDebugUtils;
    private DebugUtilsMessengerEXT debugMessenger;
#endif

    public override unsafe void Create(Sdl sdlApi)
    {
        this.CreateInstance(sdlApi);
#if DEBUG
        this.SetupDebugMessenger();
#endif
    }

    public override unsafe void Destroy()
    {
#if DEBUG
        this.extDebugUtils?.DestroyDebugUtilsMessenger(this.instance, this.debugMessenger, null);
#endif
        this.DestroyInstance();
    }

    private unsafe void CreateInstance(Sdl sdlApi)
    {
#if DEBUG
        VulkanException.ThrowsIf(!this.CheckValidationLayersSupport(), "Validation layers were requested, but none available found.");
#endif

        var applicationName = (byte*)SilkMarshal.StringToPtr("YASV");
        var engineName = (byte*)SilkMarshal.StringToPtr("No Engine");
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

        var extensions = GetRequiredExtensions(sdlApi);

        InstanceCreateInfo instanceCreateInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &applicationInfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
        };

#if DEBUG
        instanceCreateInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
        instanceCreateInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);

        var debugMessengerCreateInfo = GetDebugMessengerCreateInfo();
        instanceCreateInfo.PNext = &debugMessengerCreateInfo;
#endif

        var result = this.vk.CreateInstance(instanceCreateInfo, null, out this.instance);

        SilkMarshal.Free((nint)applicationName);
        SilkMarshal.Free((nint)engineName);
        SilkMarshal.Free((nint)instanceCreateInfo.PpEnabledExtensionNames);
#if DEBUG
        SilkMarshal.Free((nint)instanceCreateInfo.PpEnabledLayerNames);
#endif

        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create Vulkan instance: {result}.");
    }

#if DEBUG
    private unsafe bool CheckValidationLayersSupport()
    {
        uint layerCount = 0;

        var result = this.vk.EnumerateInstanceLayerProperties(&layerCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't determine instance layer properties count: {result}.");

        var availableLayersProperties = new LayerProperties[layerCount];
        fixed (LayerProperties* layerPropertiesPtr = availableLayersProperties)
        {
            result = this.vk.EnumerateInstanceLayerProperties(&layerCount, availableLayersProperties);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't enumerate instance layer properties: {result}.");
        }

        var availableLayersNames = availableLayersProperties.Select(layer => SilkMarshal.PtrToString((nint)layer.LayerName)).ToHashSet();
        return ValidationLayers.All(availableLayersNames.Contains);
    }
#endif

    private static unsafe string[] GetRequiredExtensions(Sdl sdlApi)
    {
        uint count = 0;

        var sdlBool = sdlApi.VulkanGetInstanceExtensions(null, &count, (byte**)null);
        VulkanException.ThrowsIf(sdlBool == SdlBool.False, $"Couldn't determine required extensions count: {sdlApi.GetErrorS()}.");

        var requiredExtensions = (byte**)Marshal.AllocHGlobal((nint)(sizeof(byte*) * count));
        using var defer = Disposable.Create(() => Marshal.FreeHGlobal((nint)requiredExtensions));

        sdlBool = sdlApi.VulkanGetInstanceExtensions(null, &count, requiredExtensions);
        VulkanException.ThrowsIf(sdlBool == SdlBool.False, $"Couldn't get required extensions: {sdlApi.GetErrorS()}.");

        var extensions = new List<string>(SilkMarshal.PtrToStringArray((nint)requiredExtensions, (int)count));
#if DEBUG
        extensions.Add("VK_EXT_debug_utils");
#endif
        return [.. extensions];
    }

#if DEBUG
    private static unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
                                             DebugUtilsMessageTypeFlagsEXT messageTypes,
                                             DebugUtilsMessengerCallbackDataEXT* pCallbackData,
                                             void* pUserData)
    {
        Console.WriteLine($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));
        return Vk.False;
    }

    private static unsafe DebugUtilsMessengerCreateInfoEXT GetDebugMessengerCreateInfo() => new()
    {
        SType = StructureType.DebugUtilsMessengerCreateInfoExt,
        MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
                            | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                            | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
        MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                        | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                        | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
        PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback,
        PUserData = null
    };

    private unsafe void SetupDebugMessenger()
    {
        VulkanException.ThrowsIf(!this.vk.TryGetInstanceExtension(this.instance, out this.extDebugUtils), "Couldn't get 'VK_EXT_debug_utils' extension.");

        var result = this.extDebugUtils!.CreateDebugUtilsMessenger(this.instance, GetDebugMessengerCreateInfo(), null, out this.debugMessenger);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't set up debug messenger: {result}.");
    }
#endif

    private unsafe void DestroyInstance() => this.vk.DestroyInstance(this.instance, null);
}
