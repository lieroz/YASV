using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace YASV.ViewModels;

public class SilkNETWindow : NativeControlHost
{
    IView? window = null;

    protected unsafe override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // TODO: vulkan context
        window = SdlWindowing.CreateFrom((void*)parent.Handle);

        // var sdlApi = Sdl.GetApi();

        // VkHandle instance = new();
        // VkNonDispatchableHandle surface;
        // sdlApi.VulkanCreateSurface((Silk.NET.SDL.Window*)window.Handle.ToPointer(), instance, &surface);

        window.Update += (delta) => {};
        window.Render += (delta) => {};

        Task.Run(window.Run);

        return new PlatformHandle(window.Handle, nameof(SilkNETWindow));
    }

    protected unsafe override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (window != null)
        {
            Sdl.GetApi().SetWindowSize((Silk.NET.SDL.Window*)window.Handle.ToPointer(), (int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }
}