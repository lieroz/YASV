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
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

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
    private KhrSurface? _khrSurface;
    private SurfaceKHR _surfaceKHR;
    private PhysicalDevice _physicalDevice;
    private struct QueueFamilyIndices
    {
        public uint? Graphics { get; set; }
        public uint? Present { get; set; }

        public readonly bool IsComplete() => Graphics.HasValue
                                          && Present.HasValue;
    }
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    private static readonly string[] _deviceExtensions = [
        KhrSwapchain.ExtensionName
    ];
    struct SwapchainSupportDetails
    {
        public SurfaceCapabilitiesKHR? Capabilities { get; set; }
        public SurfaceFormatKHR[]? Formats { get; set; }
        public PresentModeKHR[]? PresentModes { get; set; }
    }
    private KhrSwapchain? _khrSwapchain;
    private SwapchainKHR _swapchain;
    private Image[]? _swapchainImages;
    private Format _swapchainImageFormat;
    private Extent2D _swapchainExtent;
    private ImageView[]? _swapchainImageViews;

    public override unsafe void Create(Sdl sdlApi, IView view)
    {
        CreateInstance(sdlApi, view);
#if DEBUG
        SetupDebugMessenger();
#endif
        CreateSurface(sdlApi, view);
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain(view);
        CreateImageViews();
    }

    public override unsafe void Destroy()
    {
        DestroyImageViews();
        DestroySwapchain();
        DestroyDevice();
#if DEBUG
        _extDebugUtils?.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
#endif
        DestroySurface();
        DestroyInstance();
    }

    private unsafe void CreateInstance(Sdl sdlApi, IView view)
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

        var extensions = GetRequiredExtensions(sdlApi, view);

        InstanceCreateInfo instanceCreateInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &applicationInfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions)
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

    private static unsafe string[] GetRequiredExtensions(Sdl sdlApi, IView view)
    {
        uint count = 0;

        var sdlBool = sdlApi.VulkanGetInstanceExtensions((Silk.NET.SDL.Window*)view.Handle, &count, (byte**)null);
        VulkanException.ThrowsIf(sdlBool == SdlBool.False, $"Couldn't determine required extensions count: {sdlApi.GetErrorS()}.");

        var requiredExtensions = (byte**)Marshal.AllocHGlobal((nint)(sizeof(byte*) * count));
        using var defer = Disposable.Create(() => Marshal.FreeHGlobal((nint)requiredExtensions));

        sdlBool = sdlApi.VulkanGetInstanceExtensions((Silk.NET.SDL.Window*)view.Handle, &count, requiredExtensions);
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

    private unsafe void CreateSurface(Sdl sdlApi, IView view)
    {
        VulkanException.ThrowsIf(!_vk.TryGetInstanceExtension(_instance, out _khrSurface), "Couldn't get 'VK_KHR_sufrace' extension.");

        VkNonDispatchableHandle surfaceHandle = new();
        var result = sdlApi.VulkanCreateSurface((Silk.NET.SDL.Window*)view.Handle, new VkHandle(_instance.Handle), ref surfaceHandle);
        VulkanException.ThrowsIf(result == SdlBool.False, $"Couldn't create window surface: {sdlApi.GetErrorS()}.");

        _surfaceKHR = surfaceHandle.ToSurface();
    }

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

        for (uint i = 0; i < queueFamilyCount && !queueFamilyIndices.IsComplete(); i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndices.Graphics = i;
            }

            var result = _khrSurface!.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, _surfaceKHR, out var presentSupported);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get physical device surface support: {result}.");

            if (presentSupported)
            {
                queueFamilyIndices.Present = i;
            }
        }

        return queueFamilyIndices;
    }

    private unsafe bool CheckDeviceExtensionsSupport(PhysicalDevice physicalDevice)
    {
        uint extensionsCount = 0;
        var result = _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &extensionsCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't enumerate device extensions properties: {result}");

        var availableExtensions = new ExtensionProperties[extensionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
        {
            result = _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &extensionsCount, availableExtensionsPtr);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't enumerate device extensions properties: {result}");
        }

        var availableExtensionsNames = availableExtensions.Select(ext => SilkMarshal.PtrToString((nint)ext.ExtensionName)).ToHashSet();
        return _deviceExtensions.All(availableExtensionsNames.Contains);
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
        var extensionsSupported = CheckDeviceExtensionsSupport(physicalDevice);

        bool swapchainIsAdequate = false;
        if (extensionsSupported)
        {
            var swapchainSupport = QuerySwapChainSupport(physicalDevice);
            swapchainIsAdequate = swapchainSupport.Formats != null && swapchainSupport.PresentModes != null;
        }

        return queueFamiliyIndices.IsComplete() && swapchainIsAdequate;
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

    private unsafe void CreateLogicalDevice()
    {
        var queueFamilyIndices = FindQueueFamilies(_physicalDevice);
        var deviceQueueCreateInfos = new List<DeviceQueueCreateInfo>();
        var uniqueQueueFamilies = new[] { queueFamilyIndices.Graphics, queueFamilyIndices.Present }.Distinct().ToArray();

        var queuePriority = 1f;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            DeviceQueueCreateInfo deviceQueueCreateInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamily!.Value,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
            deviceQueueCreateInfos.Add(deviceQueueCreateInfo);
        }

        PhysicalDeviceFeatures physicalDeviceFeatures = new();

        fixed (DeviceQueueCreateInfo* deviceQueueCreateInfosPtr = deviceQueueCreateInfos.ToArray())
        {
            DeviceCreateInfo deviceCreateInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                PQueueCreateInfos = deviceQueueCreateInfosPtr,
                QueueCreateInfoCount = (uint)deviceQueueCreateInfos.Count,
                PEnabledFeatures = &physicalDeviceFeatures,
                EnabledExtensionCount = (uint)_deviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(_deviceExtensions)
            };

#if DEBUG
            deviceCreateInfo.EnabledLayerCount = (uint)_validationLayers.Length;
            deviceCreateInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_validationLayers);
#endif

            var result = _vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, out _device);

            SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledExtensionNames);
#if DEBUG
            SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
#endif

            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create logical device: {result}.");
        }

        _graphicsQueue = _vk.GetDeviceQueue(_device, queueFamilyIndices.Graphics!.Value, 0);
        _presentQueue = _vk.GetDeviceQueue(_device, queueFamilyIndices.Present!.Value, 0);
    }

    private unsafe SwapchainSupportDetails QuerySwapChainSupport(PhysicalDevice physicalDevice)
    {
        var swapchainSupportDetails = new SwapchainSupportDetails();

        var result = _khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, _surfaceKHR, out var surfaceCapabilities);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get phydical device surface capabilities: {result}.");
        swapchainSupportDetails.Capabilities = surfaceCapabilities;

        uint formatCount = 0;
        result = _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surfaceKHR, &formatCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get phydical device surface formats count: {result}.");

        if (formatCount != 0)
        {
            swapchainSupportDetails.Formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = swapchainSupportDetails.Formats)
            {
                result = _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surfaceKHR, &formatCount, formatsPtr);
                VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get phydical device surface formats: {result}.");
            }
        }

        uint presentModesCount = 0;
        result = _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surfaceKHR, &presentModesCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get phydical device surface present modes count: {result}.");

        if (presentModesCount != 0)
        {
            swapchainSupportDetails.PresentModes = new PresentModeKHR[presentModesCount];
            fixed (PresentModeKHR* presentModesPtr = swapchainSupportDetails.PresentModes)
            {
                result = _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surfaceKHR, &presentModesCount, presentModesPtr);
                VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get phydical device surface present modes: {result}.");
            }
        }

        return swapchainSupportDetails;
    }

    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (var availableFormat in availableFormats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }
        return availableFormats[0];
    }

    private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (var availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }
        return PresentModeKHR.FifoKhr;
    }

    private static unsafe Extent2D ChooseSwapExtent(IView view, SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        else
        {
            var frameBufferSize = view.FramebufferSize;

            Extent2D actualExtent = new()
            {
                Width = (uint)frameBufferSize.X,
                Height = (uint)frameBufferSize.Y
            };

            actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

            return actualExtent;
        }
    }

    private unsafe void CreateSwapchain(IView view)
    {
        var swapchainSupport = QuerySwapChainSupport(_physicalDevice);

        var surfaceFormat = ChooseSwapSurfaceFormat(swapchainSupport.Formats!);
        var presentMode = ChooseSwapPresentMode(swapchainSupport.PresentModes!);
        var extent = ChooseSwapExtent(view, swapchainSupport.Capabilities!.Value);

        uint minImageCount = swapchainSupport.Capabilities!.Value.MinImageCount;
        uint maxImageCount = swapchainSupport.Capabilities!.Value.MaxImageCount;

        uint imageCount = minImageCount + 1;
        if (minImageCount > 0 && imageCount > maxImageCount)
        {
            imageCount = maxImageCount;
        }

        SwapchainCreateInfoKHR swapchainCreateInfoKHR = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surfaceKHR,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = swapchainSupport.Capabilities!.Value.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        var queueFamilyIndices = FindQueueFamilies(_physicalDevice);
        var indices = stackalloc[] { queueFamilyIndices.Graphics!.Value, queueFamilyIndices.Present!.Value };

        if (queueFamilyIndices.Graphics != queueFamilyIndices.Present)
        {
            swapchainCreateInfoKHR.ImageSharingMode = SharingMode.Concurrent;
            swapchainCreateInfoKHR.QueueFamilyIndexCount = 2;
            swapchainCreateInfoKHR.PQueueFamilyIndices = indices;
        }
        else
        {
            swapchainCreateInfoKHR.ImageSharingMode = SharingMode.Exclusive;
        }

        VulkanException.ThrowsIf(!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain), "Couldn't get 'VK_KHR_swapchain' extension.");

        var result = _khrSwapchain!.CreateSwapchain(_device, swapchainCreateInfoKHR, null, out _swapchain);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create swapchain: {result}.");

        result = _khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, null);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get swapchain images count: {result}.");

        _swapchainImages = new Image[imageCount];
        fixed (Image* swapchainImagesPtr = _swapchainImages)
        {
            result = _khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, swapchainImagesPtr);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get swapchain images: {result}.");
        }

        _swapchainImageFormat = surfaceFormat.Format;
        _swapchainExtent = extent;
    }

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages!.Length];
        for (uint i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo imageViewCreateInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainImageFormat,
                Components = new()
                {
                    R = ComponentSwizzle.R,
                    G = ComponentSwizzle.G,
                    B = ComponentSwizzle.B,
                    A = ComponentSwizzle.A
                },
                SubresourceRange = new()
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            var result = _vk.CreateImageView(_device, &imageViewCreateInfo, null, out _swapchainImageViews[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create image view: {result}.");
        }
    }

    private unsafe void DestroyInstance() => _vk.DestroyInstance(_instance, null);

    private unsafe void DestroySurface() => _khrSurface!.DestroySurface(_instance, _surfaceKHR, null);

    private unsafe void DestroyDevice() => _vk.DestroyDevice(_device, null);

    private unsafe void DestroySwapchain() => _khrSwapchain!.DestroySwapchain(_device, _swapchain, null);

    private unsafe void DestroyImageViews()
    {
        foreach (var imageView in _swapchainImageViews!)
        {
            _vk.DestroyImageView(_device, imageView, null);
        }
    }
}
