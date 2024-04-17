namespace YASV.RHI;

using Silk.NET.SDL;

public abstract class RenderingDevice
{
    public abstract void Create(Sdl sdlApi);

    public abstract void Destroy();
}
