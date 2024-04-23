using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public abstract class GraphicsDevice(IView view)
{
    public const int MaxFramesInFlight = 2;
    protected readonly IView _view = view;

    public abstract void Create(Sdl sdlApi);

    public abstract void Destroy();

    public abstract void WaitIdle();

    public abstract void DrawFrame();
}
