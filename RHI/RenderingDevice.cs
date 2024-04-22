using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public abstract class RenderingDevice
{
    public const int MaxFramesInFlight = 2;

    public abstract void Create(Sdl sdlApi, IView view);

    public abstract void Destroy();

    public abstract void DrawFrame();
}
