using Avalonia.Controls;
using Avalonia.Platform;
using Silk.NET.Windowing;

namespace YASV.ViewModels;

public class SilkNETWindow : NativeControlHost
{
    protected unsafe override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var window = Silk.NET.Windowing.Window.Create(WindowOptions.DefaultVulkan);

        // TODO: add render, input etc. events

        return new PlatformHandle(window.Handle, nameof(SilkNETWindow));
    }
}