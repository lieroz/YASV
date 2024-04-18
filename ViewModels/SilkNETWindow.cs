using System;
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Platform;
using Silk.NET.Core.Loader;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using YASV.RHI;
using SDLThread = System.Threading.Thread;

namespace YASV.ViewModels;

public class SilkNETWindow : NativeControlHost
{
    private IView? _window;
    private RenderingDevice? _renderingDevice;
    private SDLThread? _sdlThread;
    private readonly ConcurrentQueue<Action> _sdlActions = new();

    protected override unsafe IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _window = SdlWindowing.CreateFrom((void*)parent.Handle);

        var sdlApi = Sdl.GetApi();

        if (sdlApi.VulkanLoadLibrary((byte*)null) == -1)
        {
            throw new SymbolLoadingException(sdlApi.GetErrorS());
        }

        _renderingDevice = new VulkanDevice();
        _renderingDevice.Create(sdlApi);

        _window.Update += (delta) => { };
        _window.Render += (delta) => { };

        _sdlThread = new(() =>
        {
            while (_sdlActions.TryDequeue(out var action))
            {
                action();
            }
        });
        _sdlThread.Start();

        return new PlatformHandle(_window.Handle, nameof(SilkNETWindow));
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control) => _renderingDevice?.Destroy();

    protected override unsafe void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _sdlActions.Enqueue(() => Sdl.GetApi().SetWindowSize((Silk.NET.SDL.Window*)_window!.Handle.ToPointer(), (int)e.NewSize.Width, (int)e.NewSize.Height));
    }
}
