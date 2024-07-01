using YASV.RHI;

namespace YASV.Scenes;

public abstract class BaseScene(GraphicsDevice graphicsDevice)
{
    protected GraphicsDevice _graphicsDevice = graphicsDevice;
    protected int _currentFrame = 0;

    public abstract void Draw();
}
