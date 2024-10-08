using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Platform;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using YASV.Helpers;
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
            _sdlThread!.Join();

            _graphicsDevice!.WaitIdle();

            if (disposing)
            {
                // dispose managed state (managed objects)
                CurrentScene?.Dispose();
            }

            _graphicsDevice.Destroy();

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _window.Dispose();
            _disposed = true;
        }
    }

    private bool _isMouseDown = false;
    private int _x = 0;
    private int _y = 0;

    protected override unsafe IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var vkApi = VulkanHelpers.GetApi();
        var sdlApi = Sdl.GetApi();
        sdlApi.SetHint(Sdl.HintVideoForeignWindowVulkan, "1");

        _window = SdlWindowing.CreateFrom((void*)parent.Handle);

        _graphicsDevice = new VulkanDevice(vkApi, _window);
        _graphicsDevice.Create(sdlApi);

        _sdlThread = new(() =>
        {
            _window.Run(() =>
            {
                Event ev = new();
                while (sdlApi.PollEvent(ref ev) != 0)
                {
                    switch ((EventType)ev.Type)
                    {
                        case EventType.Mousebuttondown:
                            {
                                _isMouseDown = true;
                                _x = ev.Motion.X;
                                _y = ev.Motion.Y;
                                break;
                            }
                        case EventType.Mousemotion:
                            {
                                if (_isMouseDown)
                                {
                                    var xOffset = ev.Motion.X - _x;
                                    var yOffset = ev.Motion.Y - _y;

                                    _x = ev.Motion.X;
                                    _y = ev.Motion.Y;

                                    CurrentScene?.Camera.ProcessMouseMotion(xOffset, yOffset);
                                }
                                break;
                            }
                        case EventType.Mousebuttonup:
                            {
                                _isMouseDown = false;
                                break;
                            }
                        case EventType.Mousewheel:
                            {
                                CurrentScene?.Camera.ProcessMouseWheel(ev.Wheel.Y);
                                break;
                            }
                        default:
                            break;
                    }
                }

                while (_sdlActions.TryDequeue(out var action))
                {
                    action();
                }
                CurrentScene?.DrawScene();
            });
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

    public void EnqueueAction(Action action)
    {
        _sdlActions.Enqueue(action);
    }

    public GraphicsDevice GraphicsDevice { get => _graphicsDevice!; }
}
