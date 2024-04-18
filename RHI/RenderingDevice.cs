using Silk.NET.SDL;

namespace YASV.RHI;

public abstract class RenderingDevice
{
    public abstract void Create(Sdl sdlApi);

    public abstract void Destroy();
}
