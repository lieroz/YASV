using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using SkiaSharp;

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

// https://vkguide.dev/docs/new_chapter_4/descriptor_abstractions/
internal class DescriptorAllocator
{
    private const int MaxSetsPerPool = 4092;

    internal struct PoolSizeRatio
    {
        public Silk.NET.Vulkan.DescriptorType descriptorType;
        public float ratio;
    }

    private Vk _vk;
    private Device _device;
    private List<PoolSizeRatio> _ratios = [];
    private List<DescriptorPool> _fullPools = [];
    private List<DescriptorPool> _readyPools = [];
    private int _setsPerPool = 0;

    internal DescriptorAllocator(Vk vk, Device device, int maxSets, List<PoolSizeRatio> poolRatios)
    {
        _vk = vk;
        _device = device;
        _ratios = poolRatios;
        var newPool = CreateDescriptorPool(maxSets, _ratios);
        _setsPerPool = (int)(maxSets * 1.5);
        _readyPools.Add(newPool);
    }

    public void Clear()
    {
        foreach (var pool in _readyPools)
        {
            var result = _vk.ResetDescriptorPool(_device, pool, 0);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't reset descriptor pool: '{result}'.");
        }

        foreach (var pool in _fullPools)
        {
            var result = _vk.ResetDescriptorPool(_device, pool, 0);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't reset descriptor pool: '{result}'.");
            _readyPools.Add(pool);
        }

        _fullPools.Clear();
    }

    public unsafe void Destroy()
    {
        foreach (var pool in _readyPools)
        {
            _vk.DestroyDescriptorPool(_device, pool, null);
        }

        _readyPools.Clear();

        foreach (var pool in _fullPools)
        {
            _vk.DestroyDescriptorPool(_device, pool, null);
        }

        _fullPools.Clear();
    }

    public unsafe Silk.NET.Vulkan.DescriptorSet Allocate(DescriptorSetLayout[] layouts)
    {
        var pool = GetDescriptorPool();

        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new DescriptorSetAllocateInfo()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = pool,
                DescriptorSetCount = (uint)layouts.Length,
                PSetLayouts = layoutsPtr
            };

            var result = _vk.AllocateDescriptorSets(_device, ref allocInfo, out var descriptorSet);
            if (result == Result.ErrorOutOfPoolMemory || result == Result.ErrorFragmentedPool)
            {
                _fullPools.Add(pool);

                pool = GetDescriptorPool();
                allocInfo.DescriptorPool = pool;

                result = _vk.AllocateDescriptorSets(_device, ref allocInfo, out descriptorSet);
            }

            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't allocate descriptor sets: {result}.");
            _readyPools.Add(pool);
            return descriptorSet;
        }
    }

    private DescriptorPool GetDescriptorPool()
    {
        DescriptorPool newPool;
        if (_readyPools.Count != 0)
        {
            int index = _readyPools.Count - 1;
            newPool = _readyPools.ElementAt(index);
            _readyPools.RemoveAt(index);
        }
        else
        {
            newPool = CreateDescriptorPool(_setsPerPool, _ratios);
            _setsPerPool = Math.Min(MaxSetsPerPool, (int)(_setsPerPool * 1.5));
        }

        return newPool;
    }

    private unsafe DescriptorPool CreateDescriptorPool(int setCount, IReadOnlyCollection<PoolSizeRatio> poolRatios)
    {
        var poolSizes = new DescriptorPoolSize[poolRatios.Count];
        for (int i = 0; i < poolSizes.Length; i++)
        {
            var ratio = poolRatios.ElementAt(i);
            poolSizes[i] = new()
            {
                Type = ratio.descriptorType,
                DescriptorCount = (uint)(ratio.ratio * setCount)
            };
        }

        fixed (DescriptorPoolSize* poolSizesPtr = poolSizes)
        {
            var poolCreateInfo = new DescriptorPoolCreateInfo()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = (uint)setCount,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizesPtr
            };

            var result = _vk.CreateDescriptorPool(_device, ref poolCreateInfo, null, out var newPool);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create descriptor pool: '{result}'.");
            return newPool;
        }
    }
}

internal class VulkanDescriptorWriter : DescriptorWriter
{
    private readonly List<DescriptorImageInfo> _imageInfos = [];
    private readonly List<DescriptorBufferInfo> _bufferInfos = [];
    private readonly List<WriteDescriptorSet> _imageWrites = [];
    private readonly List<WriteDescriptorSet> _bufferWrites = [];

    public unsafe void WriteImage(int binding, ImageView image, Silk.NET.Vulkan.Sampler sampler, Silk.NET.Vulkan.ImageLayout layout, Silk.NET.Vulkan.DescriptorType type)
    {
        var imageInfo = new DescriptorImageInfo()
        {
            Sampler = sampler,
            ImageView = image,
            ImageLayout = layout
        };
        _imageInfos.Add(imageInfo);

        var writeInfo = new WriteDescriptorSet()
        {
            SType = StructureType.WriteDescriptorSet,
            DstBinding = (uint)binding,
            DescriptorCount = 1,
            DescriptorType = type
        };
        _imageWrites.Add(writeInfo);
    }

    public unsafe void WriteBuffer(int binding, Silk.NET.Vulkan.Buffer buffer, int size, int offset, Silk.NET.Vulkan.DescriptorType type)
    {
        var bufferInfo = new DescriptorBufferInfo()
        {
            Buffer = buffer,
            Offset = (uint)offset,
            Range = (uint)size
        };
        _bufferInfos.Add(bufferInfo);

        var writeInfo = new WriteDescriptorSet()
        {
            SType = StructureType.WriteDescriptorSet,
            DstBinding = (uint)binding,
            DescriptorCount = 1,
            DescriptorType = type
        };
        _bufferWrites.Add(writeInfo);
    }

    public void Clear()
    {
        _imageInfos.Clear();
        _bufferInfos.Clear();

        _imageWrites.Clear();
        _bufferWrites.Clear();
    }

    public unsafe void UpdateSet(Vk vk, Device device, Silk.NET.Vulkan.DescriptorSet descriptorSet)
    {
        VulkanException.ThrowsIf(_bufferInfos.Count != _bufferWrites.Count, $"Buffer infos and writes count must be equal: {_bufferInfos.Count} != {_bufferWrites.Count}.");
        VulkanException.ThrowsIf(_imageInfos.Count != _imageWrites.Count, $"Image infos and writes count must be equal: {_imageInfos.Count} != {_imageWrites.Count}.");

        for (int i = 0; i < _bufferInfos.Count; i++)
        {
            var bufferInfo = _bufferInfos[i];
            _bufferWrites[i] = _bufferWrites[i] with { DstSet = descriptorSet, PBufferInfo = &bufferInfo };
        }

        for (int i = 0; i < _imageInfos.Count; i++)
        {
            var imageInfo = _imageInfos[i];
            _imageWrites[i] = _imageWrites[i] with { DstSet = descriptorSet, PImageInfo = &imageInfo };
        }

        _bufferWrites.AddRange(_imageWrites);
        vk.UpdateDescriptorSets(device, _bufferWrites.ToArray(), null);
    }
}

public class VulkanDevice(IView view) : GraphicsDevice(view)
{
    private readonly ShaderCompiler _shaderCompiler = new DxcShaderCompiler();
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
        public uint? Transfer { get; set; }

        public readonly bool IsComplete() => Graphics.HasValue
                                          && Present.HasValue;
    }
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    private Queue _transferQueue;
    private static string[] _deviceExtensions = [
        KhrSwapchain.ExtensionName,
        KhrDynamicRendering.ExtensionName
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
    private Silk.NET.Vulkan.Format _swapchainImageFormat;
    private Extent2D _swapchainExtent;
    private ImageView[]? _swapchainImageViews;
    private CommandPool[] _commandPools = new CommandPool[Constants.MaxFramesInFlight];
    private CommandPool _transferCommandPool;
    private Silk.NET.Vulkan.Semaphore[]? _imageAvailableSemaphores;
    private Silk.NET.Vulkan.Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private readonly ConcurrentBag<Silk.NET.Vulkan.CommandBuffer> _transferCommandBufferPool = [];
    private readonly DescriptorAllocator[] _descriptorAllocators = new DescriptorAllocator[Constants.MaxFramesInFlight];

    private static unsafe string[] GetRequiredExtensions(Sdl sdlApi, IView view)
    {
        uint count = 0;

        var sdlBool = sdlApi.VulkanGetInstanceExtensions((Silk.NET.SDL.Window*)view.Handle, &count, (byte**)null);
        VulkanException.ThrowsIf(sdlBool == SdlBool.False, $"Couldn't determine required extensions count: {sdlApi.GetErrorS()}.");

        var requiredExtensions = (byte**)Marshal.AllocHGlobal((nint)(sizeof(byte*) * count));

        sdlBool = sdlApi.VulkanGetInstanceExtensions((Silk.NET.SDL.Window*)view.Handle, &count, requiredExtensions);
        VulkanException.ThrowsIf(sdlBool == SdlBool.False, $"Couldn't get required extensions: {sdlApi.GetErrorS()}.");

        var extensions = new List<string>(SilkMarshal.PtrToStringArray((nint)requiredExtensions, (int)count));
        Marshal.FreeHGlobal((nint)requiredExtensions);
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

        var result = _vk.CreateInstance(ref instanceCreateInfo, null, out _instance);

        SilkMarshal.Free((nint)applicationName);
        SilkMarshal.Free((nint)engineName);
        SilkMarshal.Free((nint)instanceCreateInfo.PpEnabledExtensionNames);
#if DEBUG
        SilkMarshal.Free((nint)instanceCreateInfo.PpEnabledLayerNames);
#endif

        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create Vulkan instance: {result}.");
    }

    private unsafe void DestroyInstance() => _vk.DestroyInstance(_instance, null);

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

        var debugMessengerInfo = GetDebugMessengerCreateInfo();
        var result = _extDebugUtils!.CreateDebugUtilsMessenger(_instance, ref debugMessengerInfo, null, out _debugMessenger);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't set up debug messenger: {result}.");
    }

    private unsafe void DestroyDebugMessenger() => _extDebugUtils?.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);

#endif

    private unsafe void CreateSurface(Sdl sdlApi, IView view)
    {
        VulkanException.ThrowsIf(!_vk.TryGetInstanceExtension(_instance, out _khrSurface), "Couldn't get 'VK_KHR_sufrace' extension.");

        VkNonDispatchableHandle surfaceHandle = new();
        var result = sdlApi.VulkanCreateSurface((Silk.NET.SDL.Window*)view.Handle, new VkHandle(_instance.Handle), ref surfaceHandle);
        VulkanException.ThrowsIf(result == SdlBool.False, $"Couldn't create window surface: {sdlApi.GetErrorS()}.");

        _surfaceKHR = surfaceHandle.ToSurface();
    }

    private unsafe void DestroySurface() => _khrSurface!.DestroySurface(_instance, _surfaceKHR, null);

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

            // Separate queue for data transfer from CPU to GPU operations
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.TransferBit) && !queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndices.Transfer = i;
            }

            var result = _khrSurface!.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, _surfaceKHR, out var presentSupported);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't get physical device surface support: {result}.");

            if (presentSupported)
            {
                queueFamilyIndices.Present = i;
            }
        }

        if (!queueFamilyIndices.Transfer.HasValue)
        {
            queueFamilyIndices.Transfer = queueFamilyIndices.Graphics;
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
        if (availableExtensionsNames.Contains("VK_KHR_portability_subset"))
        {
            _deviceExtensions = [.. _deviceExtensions.Append("VK_KHR_portability_subset")];
        }
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

        var props = _vk.GetPhysicalDeviceFeatures(physicalDevice);
        return queueFamiliyIndices.IsComplete() && swapchainIsAdequate && props.SamplerAnisotropy;
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

        ulong maxMemorySize = 0;
        for (int i = 0; i < physicalDevices.Length; i++)
        {
            ulong memorySize = 0;
            if (IsDeviceSuitable(physicalDevices[i], ref memorySize) && memorySize > maxMemorySize)
            {
                maxMemorySize = memorySize;
                _physicalDevice = physicalDevices[i];
            }

            var props = _vk.GetPhysicalDeviceProperties(physicalDevices[i]);
            if (props.DeviceType == PhysicalDeviceType.DiscreteGpu)
            {
                _physicalDevice = physicalDevices[i];
                break;
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
        var uniqueQueueFamilies = new[] { queueFamilyIndices.Graphics, queueFamilyIndices.Present, queueFamilyIndices.Transfer }.Distinct().ToArray();

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

        PhysicalDeviceFeatures physicalDeviceFeatures = new()
        {
            SamplerAnisotropy = true
        };

        fixed (DeviceQueueCreateInfo* deviceQueueCreateInfosPtr = deviceQueueCreateInfos.ToArray())
        {
            PhysicalDeviceDynamicRenderingFeatures dynamicRenderingFeatures = new()
            {
                SType = StructureType.PhysicalDeviceDynamicRenderingFeatures,
                DynamicRendering = true
            };

            DeviceCreateInfo deviceCreateInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &dynamicRenderingFeatures,
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

            var poolSizeRatios = new List<DescriptorAllocator.PoolSizeRatio>
            {
                new() { descriptorType = Silk.NET.Vulkan.DescriptorType.StorageImage, ratio = 3 },
                new() { descriptorType = Silk.NET.Vulkan.DescriptorType.StorageBuffer, ratio = 3 },
                new() { descriptorType = Silk.NET.Vulkan.DescriptorType.UniformBuffer, ratio = 3 },
                new() { descriptorType = Silk.NET.Vulkan.DescriptorType.CombinedImageSampler, ratio = 4 }
            };

            for (int i = 0; i < Constants.MaxFramesInFlight; i++)
            {
                _descriptorAllocators[i] = new(_vk, _device, 1000, poolSizeRatios);
            }
        }

        _graphicsQueue = _vk.GetDeviceQueue(_device, queueFamilyIndices.Graphics!.Value, 0);
        _presentQueue = _vk.GetDeviceQueue(_device, queueFamilyIndices.Present!.Value, 0);
        _transferQueue = _vk.GetDeviceQueue(_device, queueFamilyIndices.Transfer!.Value, 0);
    }

    private unsafe void DestroyLogicalDevice()
    {
        for (int i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _descriptorAllocators[i].Destroy();
        }
        _vk.DestroyDevice(_device, null);
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
            if (availableFormat.Format == Silk.NET.Vulkan.Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr)
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

    private Silk.NET.Vulkan.Format FindSupportedFormat(Silk.NET.Vulkan.Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            var format = candidates[i];
            _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, format, out var props);

            if ((tiling == ImageTiling.Linear && (props.LinearTilingFeatures & features) == features)
             || (tiling == ImageTiling.Optimal && (props.OptimalTilingFeatures & features) == features))
            {
                return format;
            }
        }
        throw new NotSupportedException("Couldn't find supported format.");
    }

    private Silk.NET.Vulkan.Format FindDepthFormat()
    {
        return FindSupportedFormat([Silk.NET.Vulkan.Format.D32Sfloat, Silk.NET.Vulkan.Format.D24UnormS8Uint],
            ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
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

        var result = _khrSwapchain!.CreateSwapchain(_device, ref swapchainCreateInfoKHR, null, out _swapchain);
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

    private unsafe ImageView CreateImageViewInternal(Image image, Silk.NET.Vulkan.Format format, ImageAspectFlags aspectFlags)
    {
        var viewInfo = new ImageViewCreateInfo()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new()
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        var result = _vk.CreateImageView(_device, ref viewInfo, null, out var view);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create image view: {result}.");

        return view;
    }

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages!.Length];
        for (uint i = 0; i < _swapchainImages.Length; i++)
        {
            _swapchainImageViews[i] = CreateImageViewInternal(_swapchainImages[i], _swapchainImageFormat, ImageAspectFlags.ColorBit);
        }
    }

    private unsafe void DestroyImageViews()
    {
        foreach (var imageView in _swapchainImageViews!)
        {
            _vk.DestroyImageView(_device, imageView, null);
        }
    }

    private unsafe void CreateCommandPool()
    {
        var queueFamilyIndices = FindQueueFamilies(_physicalDevice);

        var commandPoolInfo = new CommandPoolCreateInfo()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = (uint)queueFamilyIndices.Transfer!,
        };

        var result = _vk.CreateCommandPool(_device, ref commandPoolInfo, null, out _transferCommandPool);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create command pool: {result}.");

        for (int i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            commandPoolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = (uint)queueFamilyIndices.Graphics!,
            };

            result = _vk.CreateCommandPool(_device, ref commandPoolInfo, null, out _commandPools[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create command pool: {result}.");
        }
    }

    private unsafe void DestroyCommandPools()
    {
        _vk.DestroyCommandPool(_device, _transferCommandPool, null);
        _commandPools.All(x =>
        {
            _vk.DestroyCommandPool(_device, x, null); return true;
        });
    }

    private unsafe void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Silk.NET.Vulkan.Semaphore[Constants.MaxFramesInFlight];
        _renderFinishedSemaphores = new Silk.NET.Vulkan.Semaphore[Constants.MaxFramesInFlight];
        _inFlightFences = new Fence[Constants.MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            var result = _vk.CreateSemaphore(_device, ref semaphoreInfo, null, out _imageAvailableSemaphores[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create semaphore: {result}.");

            result = _vk.CreateSemaphore(_device, ref semaphoreInfo, null, out _renderFinishedSemaphores[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create semaphore: {result}.");

            result = _vk.CreateFence(_device, ref fenceInfo, null, out _inFlightFences[i]);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create fence: {result}.");
        }
    }

    private unsafe void DestroySyncObjects()
    {
        for (int i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _vk.DestroySemaphore(_device, _imageAvailableSemaphores![i], null);
            _vk.DestroySemaphore(_device, _renderFinishedSemaphores![i], null);
            _vk.DestroyFence(_device, _inFlightFences![i], null);
        }
    }

    public override void Create(Sdl sdlApi)
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
        CreateCommandPool();
        CreateSyncObjects();
    }

    protected override void DestroyInternal()
    {
        DestroySyncObjects();
        DestroyCommandPools();

        CleanupSwapchain();

        DestroyLogicalDevice();
        DestroySurface();
#if DEBUG
        DestroyDebugMessenger();
#endif
        DestroyInstance();
    }

    private unsafe void CleanupSwapchain()
    {
        DestroyImageViews();
        DestroySwapchain();
    }

    private void RecreateSwapchain()
    {
        WaitIdle();

        CleanupSwapchain();

        CreateSwapchain(_view);
        CreateImageViews();

        RecreateTexturesAction?.Invoke((int)_swapchainExtent.Width, (int)_swapchainExtent.Height);
    }

    public override unsafe int BeginFrameInternal(int frameIndex)
    {
        _vk.WaitForFences(_device, 1, ref _inFlightFences![frameIndex], true, uint.MaxValue);

        uint imageIndex = 0;
        var result = _khrSwapchain!.AcquireNextImage(_device, _swapchain, uint.MaxValue, _imageAvailableSemaphores![frameIndex], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return -1;
        }

        VulkanException.ThrowsIf(result != Result.Success && result != Result.SuboptimalKhr, $"Couldn't acquire next image: {result}.");

        _vk.ResetFences(_device, 1, ref _inFlightFences![frameIndex]);
        _descriptorAllocators[frameIndex].Clear();

        return (int)imageIndex;
    }

    public override unsafe void EndFrameInternal(CommandBuffer commandBuffer, int frameIndex, int imageIndex)
    {
        var waitSemaphores = stackalloc[] { _imageAvailableSemaphores![frameIndex] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var buffer = commandBuffer.ToVulkanCommandBuffer();
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphores![frameIndex] };

        var submitInfo = new SubmitInfo()
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

        var result = _vk.QueueSubmit(_graphicsQueue, 1, ref submitInfo, _inFlightFences![frameIndex]);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't submit to queue: {result}.");

        var swapchains = stackalloc[] { _swapchain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapchains,
            PImageIndices = (uint*)&imageIndex,
        };

        result = _khrSwapchain!.QueuePresent(_presentQueue, ref presentInfo);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            RecreateSwapchain();
            return;
        }

        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't present image: {result}.");
    }

    public override unsafe void WaitIdle()
    {
        _vk.DeviceWaitIdle(_device);
    }

    public override Tuple<float, float> GetSwapchainSizes()
    {
        return new((int)_swapchainExtent.Width, (int)_swapchainExtent.Height);
    }

    protected override unsafe CommandBuffer[] AllocateCommandBuffers(int frameIndex, int count)
    {
        var commandBuffers = new Silk.NET.Vulkan.CommandBuffer[count];
        var allocateInfo = new CommandBufferAllocateInfo()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPools[frameIndex],
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)count
        };

        var result = _vk.AllocateCommandBuffers(_device, &allocateInfo, commandBuffers);
        VulkanException.ThrowsIf(result != Result.Success, $"vkAllocateCommandBuffers failed: {result}.");

        return commandBuffers.Select(x => new VulkanCommandBufferWrapper(x)).ToArray();
    }

    protected override void ResetCommandBuffers(int frameIndex)
    {
        var result = _vk.ResetCommandPool(_device, _commandPools[frameIndex], CommandPoolResetFlags.ReleaseResourcesBit);
        VulkanException.ThrowsIf(result != Result.Success, $"vkResetCommandPool failed: {result}.");
    }

    public override Texture GetBackBuffer(int index)
    {
        return new VulkanTextureWrapper(_swapchainImages![index], default, _swapchainImageViews![index]);
    }

    private unsafe void ImageBarrierInternal(Silk.NET.Vulkan.CommandBuffer commandBuffer, Image image, Silk.NET.Vulkan.ImageLayout oldLayout, Silk.NET.Vulkan.ImageLayout newLayout, bool hasStencilComponent = false)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            Image = image,
            SubresourceRange = new()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (newLayout == Silk.NET.Vulkan.ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SubresourceRange.AspectMask = ImageAspectFlags.DepthBit;
            if (hasStencilComponent)
            {
                barrier.SubresourceRange.AspectMask |= ImageAspectFlags.StencilBit;
            }
        }

        var srcPipelineStageFlags = PipelineStageFlags.ColorAttachmentOutputBit;
        var dstPipelineStageFlags = PipelineStageFlags.BottomOfPipeBit;

        if (oldLayout == Silk.NET.Vulkan.ImageLayout.Undefined && newLayout == Silk.NET.Vulkan.ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.None;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            srcPipelineStageFlags = PipelineStageFlags.TopOfPipeBit;
            dstPipelineStageFlags = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == Silk.NET.Vulkan.ImageLayout.TransferDstOptimal && newLayout == Silk.NET.Vulkan.ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            srcPipelineStageFlags = PipelineStageFlags.TransferBit;
            dstPipelineStageFlags = PipelineStageFlags.FragmentShaderBit;
        }
        else if (oldLayout == Silk.NET.Vulkan.ImageLayout.Undefined && newLayout == Silk.NET.Vulkan.ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.None;
            barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit;

            srcPipelineStageFlags = PipelineStageFlags.TopOfPipeBit;
            dstPipelineStageFlags = PipelineStageFlags.EarlyFragmentTestsBit;
        }

        _vk.CmdPipelineBarrier(commandBuffer, srcPipelineStageFlags, dstPipelineStageFlags, 0, 0, null, 0, null, 1, &barrier);
    }

    // TODO: add options and implement normal layout transitions
    public override unsafe void ImageBarrier(CommandBuffer commandBuffer, Texture texture, ImageLayout oldLayout, ImageLayout newLayout)
    {
        ImageBarrierInternal(commandBuffer.ToVulkanCommandBuffer(), texture.ToVulkanTexture().Image, oldLayout.ToVulkanImageLayout(), newLayout.ToVulkanImageLayout());
    }

    public override unsafe void BeginRendering(CommandBuffer commandBuffer, Texture colorTexture, Texture? depthTexture)
    {
        var clearColor = new ClearValue(new ClearColorValue(0f, 0f, 0f, 1f));

        var colorAttachmentInfo = new RenderingAttachmentInfo()
        {
            SType = StructureType.RenderingAttachmentInfoKhr,
            ImageView = colorTexture.ToVulkanTexture().ImageView,
            ImageLayout = Silk.NET.Vulkan.ImageLayout.AttachmentOptimalKhr,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor
        };

        var renderingInfo = new RenderingInfo()
        {
            SType = StructureType.RenderingInfoKhr,
            RenderArea = new()
            {
                Offset = new(0, 0),
                Extent = _swapchainExtent
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentInfo
        };

        var depthAttachmentInfo = new RenderingAttachmentInfo()
        {
            SType = StructureType.RenderingAttachmentInfoKhr
        };
        var depthClearColor = new ClearValue(new ClearColorValue(1f, 0f));

        if (depthTexture != null)
        {
            depthAttachmentInfo.ImageView = depthTexture.ToVulkanTexture().ImageView;
            depthAttachmentInfo.ImageLayout = Silk.NET.Vulkan.ImageLayout.DepthStencilAttachmentOptimal;
            depthAttachmentInfo.LoadOp = AttachmentLoadOp.Clear;
            depthAttachmentInfo.StoreOp = AttachmentStoreOp.DontCare;
            depthAttachmentInfo.ClearValue = depthClearColor;
            renderingInfo.PDepthAttachment = &depthAttachmentInfo;
        }

        _vk.CmdBeginRendering(commandBuffer.ToVulkanCommandBuffer(), ref renderingInfo);
    }

    public override void EndRendering(CommandBuffer commandBuffer)
    {
        _vk.CmdEndRendering(commandBuffer.ToVulkanCommandBuffer());
    }

    public override void BeginCommandBuffer(CommandBuffer commandBuffer)
    {
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.None,
            PInheritanceInfo = null
        };
        var result = _vk.BeginCommandBuffer(commandBuffer.ToVulkanCommandBuffer(), ref beginInfo);
        VulkanException.ThrowsIf(result != Result.Success, $"vkBeginCommandBuffer failed: {result}.");
    }

    public override void EndCommandBuffer(CommandBuffer commandBuffer)
    {
        var result = _vk.EndCommandBuffer(commandBuffer.ToVulkanCommandBuffer());
        VulkanException.ThrowsIf(result != Result.Success, $"vkEndCommandBuffer failed: {result}.");
    }

    public override unsafe Shader CreateShader(string path, ShaderStage stage)
    {
        var code = _shaderCompiler.Compile(path, stage, true);
        fixed (byte* pCode = code)
        {
            ShaderModuleCreateInfo shaderModuleCreateInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (uint)code.Length,
                PCode = (uint*)pCode,
            };

            var result = _vk.CreateShaderModule(_device, ref shaderModuleCreateInfo, null, out var shaderModule);
            VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create shader module: {result}.");

            return new VulkanShaderWrapper(shaderModule, stage);
        }
    }

    public override unsafe void DestroyShaders(Shader[] shaders)
    {
        foreach (var shader in shaders)
        {
            _vk.DestroyShaderModule(_device, shader.ToVulkanShader(), null);
        }
    }

    public override DescriptorWriter GetDescriptorWriter()
    {
        return new VulkanDescriptorWriter();
    }

    public override void BindConstantBuffer(DescriptorWriter writer, int binding, ConstantBuffer buffer, int size, int offset, DescriptorType type)
    {
        writer.ToVulkanDescriptorWriter().WriteBuffer(binding, buffer.ToVulkanConstantBuffer().Buffer, size, offset, type.ToVulkanDescriptorType());
    }

    public override void BindTexture(DescriptorWriter writer, int binding, Texture texture, TextureSampler sampler, ImageLayout layout, DescriptorType type)
    {
        writer.ToVulkanDescriptorWriter().WriteImage(binding, texture.ToVulkanTexture().ImageView, sampler.ToVulkanTextureSampler(), layout.ToVulkanImageLayout(), type.ToVulkanDescriptorType());
    }

    public override DescriptorSet GetDescriptorSet(int frameIndex, GraphicsPipelineLayout layout)
    {
        var layouts = layout.ToVulkanGraphicsPipelineLayout().DescriptorSetLayouts;
        if (layouts != null)
        {
            var descriptorSet = _descriptorAllocators[frameIndex].Allocate(layouts);
            return new VulkanDescriptorSetWrapper(descriptorSet);
        }
        throw new VulkanException("Can't allocate descriptor set with empty descriptor set layout.");
    }

    public override void UpdateDescriptorSet(DescriptorWriter writer, DescriptorSet descriptorSet)
    {
        var vkWriter = writer.ToVulkanDescriptorWriter();
        vkWriter.UpdateSet(_vk, _device, descriptorSet.ToVulkanDescriptorSet());
        vkWriter.Clear();
    }

    public override void BindDescriptorSet(CommandBuffer commandBuffer, GraphicsPipelineLayout layout, DescriptorSet set)
    {
        _vk.CmdBindDescriptorSets(commandBuffer.ToVulkanCommandBuffer(), PipelineBindPoint.Graphics, layout.ToVulkanGraphicsPipelineLayout().PipelineLayout, 0, [set.ToVulkanDescriptorSet()], null);
    }

    public override unsafe GraphicsPipelineLayout CreateGraphicsPipelineLayout(GraphicsPipelineLayoutDesc desc)
    {
        Result result = Result.Success;
        DescriptorSetLayout[]? setLayouts = null;

        if (desc.SetLayouts != null)
        {
            setLayouts = new DescriptorSetLayout[desc.SetLayouts.Length];
            for (int i = 0; i < desc.SetLayouts.Length; i++)
            {
                // TODO: move to cache with count of entities aka shared_ptr
                var createInfo = desc.SetLayouts[i].ToVulkanDescriptorSetLayout();
                result = _vk.CreateDescriptorSetLayout(_device, &createInfo, null, out var setLayout);
                VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create descriptor set layout: {result}.");

                setLayouts[i] = setLayout;
            }
        }

        PipelineLayoutCreateInfo pipelineLayout = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PSetLayouts = null,
            // TODO: Implement push constant ranges
            PushConstantRangeCount = 0,
            PPushConstantRanges = null
        };

        if (setLayouts != null)
        {
            fixed (DescriptorSetLayout* setLayoutsPtr = setLayouts)
            {
                pipelineLayout.SetLayoutCount = (uint)setLayouts.Length;
                pipelineLayout.PSetLayouts = setLayoutsPtr;
            }
        }

        result = _vk.CreatePipelineLayout(_device, ref pipelineLayout, null, out var layout);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create pipeline layout: {result}.");

        return new VulkanGraphicsPipelineLayoutWrapper(layout, setLayouts);
    }

    public override unsafe void DestroyGraphicsPipelineLayouts(GraphicsPipelineLayout[] layouts)
    {
        foreach (var layout in layouts)
        {
            var vkLayout = layout.ToVulkanGraphicsPipelineLayout();

            if (vkLayout.DescriptorSetLayouts != null)
            {
                foreach (var descriptorSetLayout in vkLayout.DescriptorSetLayouts)
                {
                    _vk.DestroyDescriptorSetLayout(_device, descriptorSetLayout, null);
                }
            }

            _vk.DestroyPipelineLayout(_device, vkLayout.PipelineLayout, null);
        }
    }

    public override unsafe GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, GraphicsPipelineLayout layout)
    {
        var shaderStages = new List<PipelineShaderStageCreateInfo>();
        foreach (var shader in desc.Shaders)
        {
            if (shader != null)
            {
                var vulkanShader = shader.ToVulkanShader();
                shaderStages.Add(new PipelineShaderStageCreateInfo()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = shader.Stage.ToVulkanShaderStage(),
                    Module = vulkanShader,
                    PName = (byte*)SilkMarshal.StringToPtr("main"),
                });
            }
        }

        var vertexInputState = desc.VertexInputState.ToVulkanVertexInputState();
        var inputAssemblyState = desc.InputAssemblyState.ToVulkanInputAssemblyState();
        var rasterizationState = desc.RasterizationState.ToVulkanRasterizationState();
        var multisampleState = desc.MultisampleState.ToVulkanMultisampleState();

        var vulkanColorBlendAttachmentStates = new PipelineColorBlendAttachmentState[desc.ColorBlendAttachmentStates.Count];
        for (int i = 0; i < desc.ColorBlendAttachmentStates.Count; i++)
        {
            vulkanColorBlendAttachmentStates[i] = desc.ColorBlendAttachmentStates[i].ToVulkanColorBlendAttachmentState();
        }
        var colorBlendState = desc.ColorBlendState.ToVulkanColorBlendState(vulkanColorBlendAttachmentStates);

        // https://docs.vulkan.org/guide/latest/dynamic_state.html
        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = null,
            ScissorCount = 1,
            PScissors = null
        };

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

        GraphicsPipelineCreateInfo pipelineInfo;
        fixed (PipelineShaderStageCreateInfo* shaderStagesPtr = shaderStages.ToArray())
        {
            fixed (Silk.NET.Vulkan.Format* formatPtr = &_swapchainImageFormat)
            {
                PipelineRenderingCreateInfo pipelineRenderingCreateInfo = new()
                {
                    SType = StructureType.PipelineRenderingCreateInfo,
                    ColorAttachmentCount = colorBlendState.AttachmentCount,
                    PColorAttachmentFormats = formatPtr
                };

                PipelineDepthStencilStateCreateInfo depthStencilState;
                if (desc.DepthStencilState != null)
                {
                    depthStencilState = ((DepthStencilState)desc.DepthStencilState).ToVulkanDepthStencilState();
                    pipelineRenderingCreateInfo.DepthAttachmentFormat = FindDepthFormat();
                    pipelineRenderingCreateInfo.StencilAttachmentFormat = Silk.NET.Vulkan.Format.Undefined;
                }

                pipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    PNext = &pipelineRenderingCreateInfo,
                    Flags = PipelineCreateFlags.None,
                    StageCount = (uint)shaderStages.Count,
                    PStages = shaderStagesPtr,
                    PVertexInputState = &vertexInputState,
                    PInputAssemblyState = &inputAssemblyState,
                    PTessellationState = null,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizationState,
                    PMultisampleState = &multisampleState,
                    PDepthStencilState = &depthStencilState,
                    PColorBlendState = &colorBlendState,
                    PDynamicState = &dynamicState,
                    Layout = layout.ToVulkanGraphicsPipelineLayout().PipelineLayout,
                    RenderPass = default,
                    Subpass = 0,
                    BasePipelineHandle = default,
                    BasePipelineIndex = -1
                };

                var result = _vk.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, out var pso);
                VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create graphics pipelines: {result}.");

                return new VulkanGraphicsPipelineWrapper(pso);
            }
        }
    }

    public override unsafe void DestroyGraphicsPipelines(GraphicsPipeline[] pipelines)
    {
        foreach (var pipeline in pipelines)
        {
            _vk.DestroyPipeline(_device, pipeline.ToVulkanGraphicsPipeline(), null);
        }
    }

    public override void BindGraphicsPipeline(CommandBuffer commandBuffer, GraphicsPipeline graphicsPipeline)
    {
        _vk.CmdBindPipeline(commandBuffer.ToVulkanCommandBuffer(), PipelineBindPoint.Graphics, graphicsPipeline.ToVulkanGraphicsPipeline());
    }

    public override void SetDefaultViewportAndScissor(CommandBuffer commandBuffer)
    {
        var viewport = new Silk.NET.Vulkan.Viewport()
        {
            X = 0,
            Y = 0,
            Width = _swapchainExtent.Width,
            Height = _swapchainExtent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        _vk.CmdSetViewport(commandBuffer.ToVulkanCommandBuffer(), 0, 1, ref viewport);

        var scissor = new Silk.NET.Vulkan.Rect2D()
        {
            Offset = new(0, 0),
            Extent = _swapchainExtent
        };
        _vk.CmdSetScissor(commandBuffer.ToVulkanCommandBuffer(), 0, 1, ref scissor);
    }

    public override void SetViewports(CommandBuffer commandBuffer, int firstViewport, Viewport[] viewports)
    {
        var vkViewports = viewports.Select(v => new Silk.NET.Vulkan.Viewport(v.X, v.Y, v.Width, v.Height, v.MinDepth, v.MaxDepth)).ToArray();
        _vk.CmdSetViewport(commandBuffer.ToVulkanCommandBuffer(), (uint)firstViewport, (uint)viewports.Length, vkViewports);
    }

    public override void SetScissors(CommandBuffer commandBuffer, int firstScissor, Rect2D[] scissors)
    {
        var vkScissors = scissors.Select(v => new Silk.NET.Vulkan.Rect2D(new(v.X, v.Y), new((uint)v.Width, (uint)v.Height))).ToArray();
        _vk.CmdSetScissor(commandBuffer.ToVulkanCommandBuffer(), (uint)firstScissor, (uint)scissors.Length, vkScissors);
    }

    public override void Draw(CommandBuffer commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        _vk.CmdDraw(commandBuffer.ToVulkanCommandBuffer(), vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public override void DrawIndexed(CommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        _vk.CmdDrawIndexed(commandBuffer.ToVulkanCommandBuffer(), indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    private int FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        var memoryProperties = _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice);

        for (int i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            var bitIsSet = (typeFilter & (1 << i)) != 0;
            if (bitIsSet && (memoryProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new VulkanException($"Couldn't find suitable memory type: {typeFilter}.");
    }

    private unsafe Tuple<Silk.NET.Vulkan.Buffer, DeviceMemory> CreateBufferInternal(int size, BufferUsageFlags usageFlags, SharingMode sharingMode, MemoryPropertyFlags memoryFlags)
    {
        var createInfo = new BufferCreateInfo()
        {
            SType = StructureType.BufferCreateInfo,
            Size = (uint)size,
            Usage = usageFlags,
            SharingMode = sharingMode
        };

        var result = _vk.CreateBuffer(_device, ref createInfo, null, out var vkBuffer);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create buffer: {result}.");

        var memoryRequirements = _vk.GetBufferMemoryRequirements(_device, vkBuffer);
        var memoryAllocateInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = (uint)FindMemoryType(memoryRequirements.MemoryTypeBits, memoryFlags)
        };

        result = _vk.AllocateMemory(_device, ref memoryAllocateInfo, null, out var vkBufferMemory);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't allocate memory for buffer: {result}.");

        result = _vk.BindBufferMemory(_device, vkBuffer, vkBufferMemory, 0);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't bind memory for buffer: {result}.");

        return new(vkBuffer, vkBufferMemory);
    }

    public override VertexBuffer CreateVertexBuffer(int size)
    {
        var (vkBuffer, vkBufferMemory) = CreateBufferInternal(size, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, MemoryPropertyFlags.DeviceLocalBit);
        return new VulkanVertexBufferWrapper(size, vkBuffer, vkBufferMemory);
    }

    public override IndexBuffer CreateIndexBuffer(int size)
    {
        var (vkBuffer, vkBufferMemory) = CreateBufferInternal(size, BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, MemoryPropertyFlags.DeviceLocalBit);
        return new VulkanIndexBufferWrapper(size, vkBuffer, vkBufferMemory);
    }

    public override ConstantBuffer CreateConstantBuffer(int size)
    {
        var (vkBuffer, vkBufferMemory) = CreateBufferInternal(size, BufferUsageFlags.UniformBufferBit, SharingMode.Exclusive, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        return new VulkanConstantBufferWrapper(size, vkBuffer, vkBufferMemory);
    }

    protected override StagingBuffer CreateStagingBuffer(int size)
    {
        var (vkBuffer, vkBufferMemory) = CreateBufferInternal(size, BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        return new VulkanStagingBufferWrapper(size, vkBuffer, vkBufferMemory);
    }

    private unsafe void DestroyBufferInternal(Silk.NET.Vulkan.Buffer buffer, DeviceMemory deviceMemory)
    {
        _vk.DestroyBuffer(_device, buffer, null);
        _vk.FreeMemory(_device, deviceMemory, null);
    }

    public override void DestroyVertexBuffer(VertexBuffer buffer)
    {
        var vkBufferWrapper = buffer.ToVulkanVertexBuffer();
        DestroyBufferInternal(vkBufferWrapper.Buffer, vkBufferWrapper.DeviceMemory);
    }

    public override void DestroyIndexBuffer(IndexBuffer buffer)
    {
        var vkBufferWrapper = buffer.ToVulkanIndexBuffer();
        DestroyBufferInternal(vkBufferWrapper.Buffer, vkBufferWrapper.DeviceMemory);
    }

    public override void DestroyConstantBuffer(ConstantBuffer buffer)
    {
        var vkBufferWrapper = buffer.ToVulkanConstantBuffer();
        DestroyBufferInternal(vkBufferWrapper.Buffer, vkBufferWrapper.DeviceMemory);
    }

    protected override void DestroyStagingBuffer(StagingBuffer buffer)
    {
        var vkBufferWrapper = buffer.ToVulkanStagingBuffer();
        DestroyBufferInternal(vkBufferWrapper.Buffer, vkBufferWrapper.DeviceMemory);
    }

    private unsafe Silk.NET.Vulkan.CommandBuffer GetTransferCommandBuffer()
    {
        if (_transferCommandBufferPool.TryTake(out var commandBuffer))
        {
            return commandBuffer;
        }

        var allocInfo = new CommandBufferAllocateInfo()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _transferCommandPool,
            CommandBufferCount = Constants.PreallocatedBuffersCount
        };

        var commandBuffers = new Silk.NET.Vulkan.CommandBuffer[Constants.PreallocatedBuffersCount];
        var result = _vk.AllocateCommandBuffers(_device, &allocInfo, commandBuffers);
        VulkanException.ThrowsIf(result != Result.Success, $"vkAllocateCommandBuffers failed: {result}.");

        for (int i = 0; i < Constants.PreallocatedBuffersCount - 1; i++)
        {
            _transferCommandBufferPool.Add(commandBuffers[i]);
        }
        return commandBuffers.Last();
    }

    private void ReturnTransferCommandBuffer(Silk.NET.Vulkan.CommandBuffer commandBuffer)
    {
        _vk.ResetCommandBuffer(commandBuffer, CommandBufferResetFlags.ReleaseResourcesBit);
        _transferCommandBufferPool.Add(commandBuffer);
    }

    private unsafe void CopyBufferInternal(Silk.NET.Vulkan.Buffer srcBuffer, Silk.NET.Vulkan.Buffer dstBuffer, int size)
    {
        var commandBuffer = GetTransferCommandBuffer();

        var beginInfo = new CommandBufferBeginInfo()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        var result = _vk.BeginCommandBuffer(commandBuffer, ref beginInfo);
        VulkanException.ThrowsIf(result != Result.Success, $"vkBeginCommandBuffer failed: {result}.");

        var copyRegion = new BufferCopy()
        {
            SrcOffset = 0,
            DstOffset = 0,
            Size = (ulong)size
        };

        _vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, [copyRegion]);
        _vk.EndCommandBuffer(commandBuffer);

        var submitInfo = new SubmitInfo()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        result = _vk.QueueSubmit(_transferQueue, 1, &submitInfo, default);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't submit to queue: {result}.");

        // TODO: rewrite to semaphores
        result = _vk.QueueWaitIdle(_transferQueue);
        VulkanException.ThrowsIf(result != Result.Success, $"vkQueueWaitIdle failed: {result}.");

        ReturnTransferCommandBuffer(commandBuffer);
    }

    private unsafe void CopyDataToDeviceLocalBuffer(Silk.NET.Vulkan.Buffer dstBuffer, byte[] data, int size)
    {
        var stagingBuffer = GetStagingBuffer(size);
        var vkStagingBuffer = stagingBuffer.ToVulkanStagingBuffer();

        void* mappedMemory = null;
        var result = _vk.MapMemory(_device, vkStagingBuffer.DeviceMemory, 0, (ulong)stagingBuffer.Size, 0, ref mappedMemory);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't map buffer memory: {result}.");

        Marshal.Copy(data, 0, (nint)mappedMemory, data.Length);
        _vk.UnmapMemory(_device, vkStagingBuffer.DeviceMemory);

        CopyBufferInternal(vkStagingBuffer.Buffer, dstBuffer, size);

        ReturnStagingBuffer(stagingBuffer);
    }

    public override unsafe void CopyDataToVertexBuffer(VertexBuffer buffer, byte[] data)
    {
        var vkBuffer = buffer.ToVulkanVertexBuffer();
        CopyDataToDeviceLocalBuffer(vkBuffer.Buffer, data, buffer.Size);
    }

    public override unsafe void CopyDataToIndexBuffer(IndexBuffer buffer, byte[] data)
    {
        var vkBuffer = buffer.ToVulkanIndexBuffer();
        CopyDataToDeviceLocalBuffer(vkBuffer.Buffer, data, buffer.Size);
    }

    public override unsafe void CopyDataToConstantBuffer(ConstantBuffer buffer, byte[] data)
    {
        var vkBuffer = buffer.ToVulkanConstantBuffer();

        void* mappedMemory = null;
        var result = _vk.MapMemory(_device, vkBuffer.DeviceMemory, 0, (ulong)vkBuffer.Size, 0, ref mappedMemory);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't map buffer memory: {result}.");

        Marshal.Copy(data, 0, (nint)mappedMemory, data.Length);
        _vk.UnmapMemory(_device, vkBuffer.DeviceMemory);
    }

    public override unsafe void BindVertexBuffers(CommandBuffer commandBuffer, VertexBuffer[] buffers)
    {
        var vkBuffers = buffers.Select(x => x.ToVulkanVertexBuffer().Buffer).ToArray();
        var offsets = new ulong[vkBuffers.Length];
        Array.Fill<ulong>(offsets, 0);

        fixed (Silk.NET.Vulkan.Buffer* buffersPtr = vkBuffers)
        {
            _vk.CmdBindVertexBuffers(commandBuffer.ToVulkanCommandBuffer(), 0, (uint)buffers.Length, buffersPtr, offsets);
        }
    }

    public override unsafe void BindIndexBuffer(CommandBuffer commandBuffer, IndexBuffer buffer, IndexType indexType)
    {
        _vk.CmdBindIndexBuffer(commandBuffer.ToVulkanCommandBuffer(), buffer.ToVulkanIndexBuffer().Buffer, 0, indexType.ToVulkanIndexType());
    }

    private unsafe void CopyBufferToImageInternal(Silk.NET.Vulkan.CommandBuffer commandBuffer, Silk.NET.Vulkan.Buffer buffer, Image image, int width, int height)
    {
        var copyRegion = new BufferImageCopy()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new(0, 0, 0),
            ImageExtent = new((uint)width, (uint)height, 1)
        };

        _vk.CmdCopyBufferToImage(commandBuffer, buffer, image, Silk.NET.Vulkan.ImageLayout.TransferDstOptimal, 1, [copyRegion]);
    }

    private unsafe Tuple<Image, DeviceMemory> CreateImage(int width, int height, Silk.NET.Vulkan.Format format, ImageTiling tiling, ImageUsageFlags usageFlags, MemoryPropertyFlags memoryPropertyFlags)
    {
        var imageInfo = new ImageCreateInfo()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new()
            {
                Width = (uint)width,
                Height = (uint)height,
                Depth = 1
            },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = Silk.NET.Vulkan.ImageLayout.Undefined,
            Usage = usageFlags,
            Samples = Silk.NET.Vulkan.SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        var result = _vk.CreateImage(_device, ref imageInfo, null, out var vkImage);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create image: {result}.");

        var memoryRequirements = _vk.GetImageMemoryRequirements(_device, vkImage);
        var allocInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = (uint)FindMemoryType(memoryRequirements.MemoryTypeBits, memoryPropertyFlags)
        };

        result = _vk.AllocateMemory(_device, ref allocInfo, null, out var vkImageMemory);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't allocate image memory: {result}.");

        result = _vk.BindImageMemory(_device, vkImage, vkImageMemory, 0);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't bind image memory: {result}.");

        return new(vkImage, vkImageMemory);
    }

    public override unsafe Texture CreateTextureFromImage(SKImage image)
    {
        var imageSize = image.Info.BytesSize;
        var stagingBuffer = GetStagingBuffer(imageSize);
        var vkStagingBuffer = stagingBuffer.ToVulkanStagingBuffer();
        var vkFormat = image.ColorType.ToVulkanFormat();

        void* mappedMemory = null;
        var result = _vk.MapMemory(_device, vkStagingBuffer.DeviceMemory, 0, (ulong)stagingBuffer.Size, 0, ref mappedMemory);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't map buffer memory: {result}.");

        var bitmap = SKBitmap.Decode(image.EncodedData);
        Marshal.Copy(bitmap.Bytes, 0, (nint)mappedMemory, imageSize);

        _vk.UnmapMemory(_device, vkStagingBuffer.DeviceMemory);

        ReturnStagingBuffer(stagingBuffer);

        var (texture, memory) = CreateImage(
            image.Width,
            image.Height,
            vkFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            MemoryPropertyFlags.DeviceLocalBit);

        var commandBuffer = GetTransferCommandBuffer();

        var beginInfo = new CommandBufferBeginInfo()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        result = _vk.BeginCommandBuffer(commandBuffer, ref beginInfo);
        VulkanException.ThrowsIf(result != Result.Success, $"vkBeginCommandBuffer failed: {result}.");

        ImageBarrierInternal(commandBuffer, texture, Silk.NET.Vulkan.ImageLayout.Undefined, Silk.NET.Vulkan.ImageLayout.TransferDstOptimal);
        CopyBufferToImageInternal(commandBuffer, vkStagingBuffer.Buffer, texture, image.Width, image.Height);
        ImageBarrierInternal(commandBuffer, texture, Silk.NET.Vulkan.ImageLayout.TransferDstOptimal, Silk.NET.Vulkan.ImageLayout.ShaderReadOnlyOptimal);

        _vk.EndCommandBuffer(commandBuffer);

        var submitInfo = new SubmitInfo()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        result = _vk.QueueSubmit(_transferQueue, 1, &submitInfo, default);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't submit to queue: {result}.");

        // TODO: rewrite to semaphores
        result = _vk.QueueWaitIdle(_transferQueue);
        VulkanException.ThrowsIf(result != Result.Success, $"vkQueueWaitIdle failed: {result}.");

        ReturnTransferCommandBuffer(commandBuffer);

        var view = CreateImageViewInternal(texture, vkFormat, ImageAspectFlags.ColorBit);

        return new VulkanTextureWrapper(texture, memory, view);
    }

    public override Texture CreateTexture(int width, int height, Format format)
    {
        var usageFlags = ImageUsageFlags.ColorAttachmentBit;
        var aspectFlags = ImageAspectFlags.ColorBit;

        if (format == Format.D32_Float || format == Format.D24_Unorm_S8_Uint)
        {
            usageFlags = ImageUsageFlags.DepthStencilAttachmentBit;
            aspectFlags = ImageAspectFlags.DepthBit;

            if (format == Format.D24_Unorm_S8_Uint)
            {
                aspectFlags |= ImageAspectFlags.StencilBit;
            }
        }

        var vkFormat = format.ToVulkanFormat();
        var (image, memory) = CreateImage(
            width,
            height,
            vkFormat,
            ImageTiling.Optimal,
            usageFlags,
            MemoryPropertyFlags.DeviceLocalBit);
        var imageView = CreateImageViewInternal(image, vkFormat, aspectFlags);
        return new VulkanTextureWrapper(image, memory, imageView);
    }

    public override unsafe void DestoryTexture(Texture texture)
    {
        var vkTexture = texture.ToVulkanTexture();
        _vk.DestroyImageView(_device, vkTexture.ImageView, null);
        _vk.DestroyImage(_device, vkTexture.Image, null);
        _vk.FreeMemory(_device, vkTexture.DeviceMemory, null);
    }

    public override unsafe TextureSampler CreateTextureSampler(TextureSamplerDesc desc)
    {
        var properties = _vk.GetPhysicalDeviceProperties(_physicalDevice);
        var samplerInfo = desc.ToVulkanSampler(desc.AnisotropyEnable ? properties.Limits.MaxSamplerAnisotropy : 0);
        var result = _vk.CreateSampler(_device, ref samplerInfo, null, out var sampler);
        VulkanException.ThrowsIf(result != Result.Success, $"Couldn't create sampler: '{result}'.");

        return new VulkanTextureSamplerWrapper(sampler);
    }

    public override unsafe void DestroyTextureSampler(TextureSampler textureSampler)
    {
        _vk.DestroySampler(_device, textureSampler.ToVulkanTextureSampler(), null);
    }
}
