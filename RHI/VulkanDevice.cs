using System;
using System.Collections.Generic;
using System.IO;
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

// TODO: Add shader compilation code via dxc
// TODO: Use HLSL shaders
public class VulkanDevice(IView view) : GraphicsDevice(view)
{
    private readonly Vk _vk = Vk.GetApi();
    private Instance _instance;
#if DEBUG
    private ExtDebugUtils? _extDebugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private static readonly string[] _validationLayers = [
        "VK_LAYER_KHRONOS_profiles",
        "VK_LAYER_KHRONOS_validation",
    ];
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
    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _graphicsPipeline;
    private Framebuffer[]? _swapchainFramebuffers;
    private CommandPool _commandPool;
    private CommandBuffer[]? _commandBuffers;
    private Silk.NET.Vulkan.Semaphore[]? _imageAvailableSemaphores;
    private Silk.NET.Vulkan.Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private uint _currentFrame = 0;

    public override unsafe void Create(Sdl sdlApi)
    {
        CreateInstance(sdlApi, _view);
#if DEBUG
        SetupDebugMessenger();
#endif
        CreateSurface(sdlApi, _view);
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain(_view);
        CreateImageViews();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateFramebuffers();
        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    public override unsafe void Destroy()
    {
        DestroySyncObjects();
        FreeCommandBuffers();
        DestroyCommandPool();
        DestroyGraphicsPipeline();
        DestroyRenderPass();

        CleanupSwapchain();

        DestroyLogicalDevice();
        DestroySurface();
#if DEBUG
        DestroyDebugMessenger();
#endif
        DestroyInstance();
    }

    public override unsafe void WaitIdle()
    {
        _vk.DeviceWaitIdle(_device);
    }

    private unsafe void CleanupSwapchain()
    {
        DestroyFramebuffers();
        DestroyImageViews();
        DestroySwapchain();
    }

    // TODO: Recreate render passes
    private void RecreateSwapchain()
    {
        WaitIdle();

        CleanupSwapchain();

        CreateSwapchain(_view);
        CreateImageViews();
        CreateFramebuffers();
    }

    public override unsafe void DrawFrame()
    {
        _vk.WaitForFences(_device, 1, _inFlightFences![_currentFrame], true, uint.MaxValue);

        uint imageIndex = 0;
        var result = _khrSwapchain!.AcquireNextImage(_device, _swapchain, uint.MaxValue, _imageAvailableSemaphores![_currentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        VulkanException.ThrowsIf(result != Result.Success && result != Result.SuboptimalKhr, $"Couldn't acquire next image: {result}.");

        _vk.ResetFences(_device, 1, _inFlightFences![_currentFrame]);

        result = _vk.ResetCommandBuffer(_commandBuffers![_currentFrame], CommandBufferResetFlags.None);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't reset command buffer: {result}.");

        RecordCommandBuffer(_commandBuffers[_currentFrame], imageIndex);

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores![_currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var buffer = _commandBuffers[_currentFrame];
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![_currentFrame] };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &buffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        result = _vk.QueueSubmit(_graphicsQueue, 1, submitInfo, _inFlightFences[_currentFrame]);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't submit to queue: {result}.");

        var swapchains = stackalloc[] { _swapchain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapchains,
            PImageIndices = &imageIndex,
        };

        result = _khrSwapchain.QueuePresent(_graphicsQueue, presentInfo);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            RecreateSwapchain();
            return;
        }

        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't present image: {result}.");

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    #region Instance

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

    private unsafe void DestroyInstance() => _vk.DestroyInstance(_instance, null);

    #endregion

    #region Validation Layers

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

    private unsafe void DestroyDebugMessenger() => _extDebugUtils?.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);

#endif
    #endregion

    #region Surface

    private unsafe void CreateSurface(Sdl sdlApi, IView view)
    {
        VulkanException.ThrowsIf(!_vk.TryGetInstanceExtension(_instance, out _khrSurface), "Couldn't get 'VK_KHR_sufrace' extension.");

        VkNonDispatchableHandle surfaceHandle = new();
        var result = sdlApi.VulkanCreateSurface((Silk.NET.SDL.Window*)view.Handle, new VkHandle(_instance.Handle), ref surfaceHandle);
        VulkanException.ThrowsIf(result == SdlBool.False, $"Couldn't create window surface: {sdlApi.GetErrorS()}.");

        _surfaceKHR = surfaceHandle.ToSurface();
    }

    private unsafe void DestroySurface() => _khrSurface!.DestroySurface(_instance, _surfaceKHR, null);

    #endregion

    #region Physical Device

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

    #endregion

    #region Logical Device

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

    private unsafe void DestroyLogicalDevice() => _vk.DestroyDevice(_device, null);

    #endregion

    #region Swapchain
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

    private unsafe void DestroySwapchain() => _khrSwapchain!.DestroySwapchain(_device, _swapchain, null);

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

    private unsafe void DestroyImageViews()
    {
        foreach (var imageView in _swapchainImageViews!)
        {
            _vk.DestroyImageView(_device, imageView, null);
        }
    }

    #endregion

    #region Render Pass

    private unsafe void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentReference = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpassDescription = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentReference
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpassDescription,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        var result = _vk.CreateRenderPass(_device, renderPassInfo, null, out _renderPass);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create render pass: {result}.");
    }

    private unsafe void DestroyRenderPass() => _vk.DestroyRenderPass(_device, _renderPass, null);

    #endregion

    #region Graphics Pipeline State

    private unsafe ShaderModule CreateShaderModule(byte[] code)
    {
        fixed (byte* codePtr = code)
        {
            ShaderModuleCreateInfo shaderModuleCreateInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (uint)code.Length,
                PCode = (uint*)codePtr,
            };

            ShaderModule shaderModule = new();
            var result = _vk.CreateShaderModule(_device, shaderModuleCreateInfo, null, out shaderModule);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create shader module: {result}.");

            return shaderModule;
        }
    }

    private unsafe void CreateGraphicsPipeline()
    {
        var vertShaderCode = File.ReadAllBytes("Shaders/triangle.vert.spv");
        var fragShaderCode = File.ReadAllBytes("Shaders/triangle.frag.spv");

        var vertShaderModule = CreateShaderModule(vertShaderCode);
        var fragShaderModule = CreateShaderModule(fragShaderCode);

        PipelineShaderStageCreateInfo vertShaderStage = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        PipelineShaderStageCreateInfo fragShaderStage = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        var shaderStages = stackalloc[] { vertShaderStage, fragShaderStage };

        PipelineVertexInputStateCreateInfo vertexInputState = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
        };

        PipelineInputAssemblyStateCreateInfo inputAssemblyState = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        Viewport viewport = new()
        {
            X = 0f,
            Y = 0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0f,
            MaxDepth = 1f
        };

        Rect2D scissor = new()
        {
            Offset = new(0, 0),
            Extent = _swapchainExtent
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor
        };

        PipelineRasterizationStateCreateInfo rasterizationState = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false,
            DepthBiasConstantFactor = 0f,
            DepthBiasClamp = 0f,
            DepthBiasSlopeFactor = 0f
        };

        PipelineMultisampleStateCreateInfo multisampleState = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            MinSampleShading = 1f,
            PSampleMask = null,
            AlphaToCoverageEnable = false,
            AlphaToOneEnable = false
        };

        PipelineColorBlendAttachmentState colorBlendAttachmentState = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit
                           | ColorComponentFlags.GBit
                           | ColorComponentFlags.BBit
                           | ColorComponentFlags.ABit,
            BlendEnable = false,
            SrcColorBlendFactor = Silk.NET.Vulkan.BlendFactor.One,
            DstColorBlendFactor = Silk.NET.Vulkan.BlendFactor.Zero,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = Silk.NET.Vulkan.BlendFactor.One,
            DstAlphaBlendFactor = Silk.NET.Vulkan.BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add
        };

        PipelineColorBlendStateCreateInfo colorBlendState = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachmentState
        };

        colorBlendState.BlendConstants[0] = 0f;
        colorBlendState.BlendConstants[1] = 0f;
        colorBlendState.BlendConstants[2] = 0f;
        colorBlendState.BlendConstants[3] = 0f;

        var dynamicStates = stackalloc[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        };
        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 2,
            PDynamicStates = dynamicStates
        };

        PipelineLayoutCreateInfo pipelineLayout = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PSetLayouts = null,
            PushConstantRangeCount = 0,
            PPushConstantRanges = null
        };

        var result = _vk.CreatePipelineLayout(_device, pipelineLayout, null, out _pipelineLayout);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create pipeline layout: {result}.");

        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = shaderStages,
            PVertexInputState = &vertexInputState,
            PInputAssemblyState = &inputAssemblyState,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizationState,
            PMultisampleState = &multisampleState,
            PDepthStencilState = null,
            PColorBlendState = &colorBlendState,
            PDynamicState = &dynamicState,
            Layout = _pipelineLayout,
            RenderPass = _renderPass,
            Subpass = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1
        };

        result = _vk.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, out _graphicsPipeline);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create graphics pipelines: {result}.");

        SilkMarshal.Free((nint)vertShaderStage.PName);
        SilkMarshal.Free((nint)fragShaderStage.PName);

        _vk.DestroyShaderModule(_device, vertShaderModule, null);
        _vk.DestroyShaderModule(_device, fragShaderModule, null);
    }

    private unsafe void DestroyGraphicsPipeline()
    {
        _vk.DestroyPipeline(_device, _graphicsPipeline, null);
        _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
    }

    #endregion

    #region Framebuffer

    private unsafe void CreateFramebuffers()
    {
        _swapchainFramebuffers = new Framebuffer[_swapchainImageViews!.Length];
        for (uint i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachments = stackalloc[] { _swapchainImageViews[i] };

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = attachments,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };

            var result = _vk.CreateFramebuffer(_device, &framebufferInfo, null, out _swapchainFramebuffers[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create framebuffer[{i}]: {result}.");
        }
    }

    private unsafe void DestroyFramebuffers()
    {
        foreach (var framebuffer in _swapchainFramebuffers!)
        {
            _vk.DestroyFramebuffer(_device, framebuffer, null);
        }
    }

    #endregion

    #region Command Pool

    private unsafe void CreateCommandPool()
    {
        var queueFamilyIndices = FindQueueFamilies(_physicalDevice);

        CommandPoolCreateInfo commandPoolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = (uint)queueFamilyIndices.Graphics!,
        };

        var result = _vk.CreateCommandPool(_device, commandPoolInfo, null, out _commandPool);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create command pool: {result}.");
    }

    private unsafe void DestroyCommandPool() => _vk.DestroyCommandPool(_device, _commandPool, null);

    private unsafe void CreateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[MaxFramesInFlight];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = MaxFramesInFlight
        };

        var result = _vk.AllocateCommandBuffers(_device, &allocateInfo, _commandBuffers);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't allocate command buffers: {result}.");
    }

    private unsafe void FreeCommandBuffers() => _vk.FreeCommandBuffers(_device, _commandPool, _commandBuffers);

    private unsafe void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
    {
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.None,
            PInheritanceInfo = null
        };

        var result = _vk.BeginCommandBuffer(commandBuffer, beginInfo);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't begin command buffer: {result}.");

        var clearColor = new ClearValue(new ClearColorValue(1f, 0f, 0f, 1f));
        RenderPassBeginInfo renderPassBeginInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _swapchainFramebuffers![imageIndex],
            RenderArea = new()
            {
                Offset = new(0, 0),
                Extent = _swapchainExtent
            },
            ClearValueCount = 1,
            PClearValues = &clearColor
        };

        _vk.CmdBeginRenderPass(commandBuffer, renderPassBeginInfo, SubpassContents.Inline);

        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);

        Viewport viewport = new()
        {
            X = 0f,
            Y = 0f,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0f,
            MaxDepth = 1f
        };
        _vk.CmdSetViewport(commandBuffer, 0, 1, viewport);

        Rect2D scissor = new()
        {
            Offset = new(0, 0),
            Extent = _swapchainExtent
        };
        _vk.CmdSetScissor(commandBuffer, 0, 1, scissor);

        _vk.CmdDraw(commandBuffer, 3, 1, 0, 0);

        _vk.CmdEndRenderPass(commandBuffer);

        result = _vk.EndCommandBuffer(commandBuffer);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't end command buffer: {result}.");
    }

    #endregion

    #region Synchronization Objects

    private unsafe void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Silk.NET.Vulkan.Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Silk.NET.Vulkan.Semaphore[MaxFramesInFlight];
        _inFlightFences = new Fence[MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            var result = _vk.CreateSemaphore(_device, semaphoreInfo, null, out _imageAvailableSemaphores[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create semaphore: {result}.");

            result = _vk.CreateSemaphore(_device, semaphoreInfo, null, out _renderFinishedSemaphores[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create semaphore: {result}.");

            result = _vk.CreateFence(_device, fenceInfo, null, out _inFlightFences[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create fence: {result}.");
        }
    }

    private unsafe void DestroySyncObjects()
    {
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _vk.DestroySemaphore(_device, _imageAvailableSemaphores![i], null);
            _vk.DestroySemaphore(_device, _renderFinishedSemaphores![i], null);
            _vk.DestroyFence(_device, _inFlightFences![i], null);
        }
    }

    #endregion
}
