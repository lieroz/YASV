using System;
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Platform;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using YASV.RHI;
using YASV.Scenes;
using SDLThread = System.Threading.Thread;

namespace YASV.ViewModels;

public class SilkNETWindow : NativeControlHost, IDisposable
{
    private bool _disposed = false;
    private IView? _window;
    private GraphicsDevice? _graphicsDevice;
    private SDLThread? _sdlThread;
    private readonly ConcurrentQueue<Action> _sdlActions = new();

    public BaseScene? CurrentScene { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _sdlActions.Enqueue(_window!.Close);

            if (disposing)
            {
                // dispose managed state (managed objects)
                CurrentScene?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _sdlThread!.Join();
            _window.Dispose();
            _disposed = true;
        }
    }

    protected override unsafe IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var sdlApi = Sdl.GetApi();
        sdlApi.SetHint(Sdl.HintVideoForeignWindowVulkan, "1");

        // TODO: Add packaging of vulkan-1 library
        _window = SdlWindowing.CreateFrom((void*)parent.Handle);

        _graphicsDevice = new VulkanDevice(_window);
        _graphicsDevice.Create(sdlApi);

        _sdlThread = new(() =>
        {
            _window.Run(() =>
            {
                while (_sdlActions.TryDequeue(out var action) && !_window.IsClosing)
                {
                    action();
                }
                if (CurrentScene != null)
                {
                    CurrentScene.Draw();
                    _graphicsDevice.DrawFrame(); // TODO: move to Draw in each Scene
                }
            });
            _graphicsDevice.WaitIdle();
            _graphicsDevice.Destroy();
        })
        {
            Name = "SDLThread"
        };
        _sdlThread.Start();

        return new PlatformHandle(_window.Handle, nameof(SilkNETWindow));
    }

    protected override unsafe void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _sdlActions.Enqueue(() => Sdl.GetApi().SetWindowSize((Silk.NET.SDL.Window*)_window!.Handle, (int)e.NewSize.Width, (int)e.NewSize.Height));
    }

    public GraphicsDevice GraphicsDevice { get => _graphicsDevice!; }
}
