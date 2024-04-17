namespace YASV.ViewModels;

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

public class SilkNETWindow : NativeControlHost
{
    private IView? window;
    private RenderingDevice? renderingDevice;
    private SDLThread? sdlThread;
    private readonly ConcurrentQueue<Action> sdlActions = new();

    protected override unsafe IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        this.window = SdlWindowing.CreateFrom((void*)parent.Handle);

        var sdlApi = Sdl.GetApi();

        if (sdlApi.VulkanLoadLibrary((byte*)null) == -1)
        {
            throw new SymbolLoadingException(sdlApi.GetErrorS());
        }

        this.renderingDevice = new VulkanDevice();
        this.renderingDevice.Create(sdlApi);

        this.window.Update += (delta) => { };
        this.window.Render += (delta) => { };

        this.sdlThread = new(() =>
        {
            while (this.sdlActions.TryDequeue(out var action))
            {
                action();
            }
        });
        this.sdlThread.Start();

        return new PlatformHandle(this.window.Handle, nameof(SilkNETWindow));
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control) => this.renderingDevice?.Destroy();

    protected override unsafe void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        this.sdlActions.Enqueue(() => Sdl.GetApi().SetWindowSize((Silk.NET.SDL.Window*)this.window!.Handle.ToPointer(), (int)e.NewSize.Width, (int)e.NewSize.Height));
    }
}
