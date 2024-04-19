using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public abstract class RenderingDevice
{
    public abstract void Create(Sdl sdlApi, IView view);

    public abstract void Destroy();
}
