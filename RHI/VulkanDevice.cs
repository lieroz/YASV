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

namespace YASV.RHI;

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
    private static readonly string[] _validationLayers = [
        "VK_LAYER_KHRONOS_profiles",
        "VK_LAYER_KHRONOS_validation",
    ];
#endif

    private readonly Vk _vk = Vk.GetApi();
    private Instance _instance;
#if DEBUG
    private ExtDebugUtils? _extDebugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
#endif
    private PhysicalDevice _physicalDevice;
    private struct QueueFamilyIndices
    {
        public int? Graphics { get; set; }

        public readonly bool IsComplete() => Graphics.HasValue;
    }

    public override unsafe void Create(Sdl sdlApi)
    {
        CreateInstance(sdlApi);
#if DEBUG
        SetupDebugMessenger();
#endif
        PickPhysicalDevice();
    }

    public override unsafe void Destroy()
    {
#if DEBUG
        _extDebugUtils?.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
#endif
        DestroyInstance();
    }

    private unsafe void CreateInstance(Sdl sdlApi)
    {
#if DEBUG
        VulkanException.ThrowsIf(!CheckValidationLayersSupport(), "Validation layers were requested, but none available found.");
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
        instanceCreateInfo.EnabledLayerCount = (uint)_validationLayers.Length;
        instanceCreateInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_validationLayers);

        var debugMessengerCreateInfo = GetDebugMessengerCreateInfo();
        instanceCreateInfo.PNext = &debugMessengerCreateInfo;
#endif

        var result = _vk.CreateInstance(instanceCreateInfo, null, out _instance);

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

        var result = _vk.EnumerateInstanceLayerProperties(&layerCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't determine instance layer properties count: {result}.");

        var availableLayersProperties = new LayerProperties[layerCount];
        fixed (LayerProperties* layerPropertiesPtr = availableLayersProperties)
        {
            result = _vk.EnumerateInstanceLayerProperties(&layerCount, availableLayersProperties);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't enumerate instance layer properties: {result}.");
        }

        var availableLayersNames = availableLayersProperties.Select(layer => SilkMarshal.PtrToString((nint)layer.LayerName)).ToHashSet();
        return _validationLayers.All(availableLayersNames.Contains);
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
        VulkanException.ThrowsIf(!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils), "Couldn't get 'VK_EXT_debug_utils' extension.");

        var result = _extDebugUtils!.CreateDebugUtilsMessenger(_instance, GetDebugMessengerCreateInfo(), null, out _debugMessenger);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't set up debug messenger: {result}.");
    }
#endif

    private unsafe QueueFamilyIndices FindQueueFamilies(PhysicalDevice physicalDevice)
    {
        QueueFamilyIndices queueFamilyIndices = new();

        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamiliesPtr);
        }

        for (int i = 0; i < queueFamilyCount && !queueFamilyIndices.IsComplete(); i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndices.Graphics = i;
            }
        }

        return queueFamilyIndices;
    }

    private unsafe bool IsDeviceSuitable(PhysicalDevice physicalDevice, ref ulong memorySize)
    {
        var memoryProperties = _vk.GetPhysicalDeviceMemoryProperties(physicalDevice);
        foreach (var heap in memoryProperties.MemoryHeaps.AsSpan())
        {
            if (heap.Flags.HasFlag(MemoryHeapFlags.DeviceLocalBit))
            {
                memorySize += heap.Size;
            }
        }

        var queueFamiliyIndices = FindQueueFamilies(physicalDevice);
        return queueFamiliyIndices.IsComplete();
    }

    private unsafe void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        var result = _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't enumerate physical devices: {result}.");
        VulkanException.ThrowsIf(deviceCount == 0, "Couldn't find any GPUs with Vulkan API support.");

        var physicalDevices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* physicalDevicesPtr = physicalDevices)
        {
            result = _vk.EnumeratePhysicalDevices(_instance, &deviceCount, physicalDevicesPtr);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get physical devices: {result}.");
        }

        // Picking GPU with most memory
        // Cause most of the time the more memory GPU has the more performant it is
        // Ideally all GPU parameters have to be taken into account
        ulong maxMemorySize = 0;
        for (int i = 0; i < physicalDevices.Length; i++)
        {
            ulong memorySize = 0;
            if (IsDeviceSuitable(physicalDevices[i], ref memorySize) && memorySize > maxMemorySize)
            {
                maxMemorySize = memorySize;
                _physicalDevice = physicalDevices[i];
            }
        }

        VulkanException.ThrowsIf(_physicalDevice.Handle == 0, "Couldn't find suitable GPU.");

        var deviceProperties = _vk.GetPhysicalDeviceProperties(_physicalDevice);
        Console.WriteLine($"DeviceName: {SilkMarshal.PtrToString((nint)deviceProperties.DeviceName)}\n" +
            $"\tApiVersion: {deviceProperties.ApiVersion}\n" +
            $"\tDeviceID: {deviceProperties.DeviceID}\n" +
            $"\tDeviceType: {deviceProperties.DeviceType}\n" +
            $"\tDriverVersion: {deviceProperties.DriverVersion}\n" +
            $"\tVendorID: {deviceProperties.VendorID}\n" +
            $"\tMemorySize: {(double)maxMemorySize / 1024 / 1024 / 1024:F3} Gib\n");
    }

    private unsafe void DestroyInstance() => _vk.DestroyInstance(_instance, null);
}
