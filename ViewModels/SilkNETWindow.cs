using System;
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Platform;
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

        var sdlApi = Sdl.GetApi();
        sdlApi.SetHint(Sdl.HintVideoForeignWindowVulkan, 1);

        _window = SdlWindowing.CreateFrom((void*)parent.Handle);
        _window.Initialize();

        _renderingDevice = new VulkanDevice();
        _renderingDevice.Create(sdlApi, _window);

        _window.Update += (delta) => { };
        _window.Render += (delta) =>
        {
            // _renderingDevice.DrawFrame();
        };

        _sdlThread = new(() =>
        {
            _window.Run(() =>
            {
                while (_sdlActions.TryDequeue(out var action))
                {
                    action();
                }
                _renderingDevice.DrawFrame();
            });
        });
        _sdlThread.Start();

        return new PlatformHandle(_window.Handle, nameof(SilkNETWindow));
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control) => _renderingDevice?.Destroy();

    protected override unsafe void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _sdlActions.Enqueue(() => Sdl.GetApi().SetWindowSize((Silk.NET.SDL.Window*)_window!.Handle, (int)e.NewSize.Width, (int)e.NewSize.Height));
    }
}
