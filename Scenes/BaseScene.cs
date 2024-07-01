using System;
using YASV.RHI;

namespace YASV.Scenes;

public abstract class BaseScene(GraphicsDevice graphicsDevice) : IDisposable
{
    protected GraphicsDevice _graphicsDevice = graphicsDevice;
    protected int _currentFrame = 0;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract void Dispose(bool disposing);
    public abstract void Draw();
}
