using System;
using YASV.RHI;

namespace YASV.Scenes;

public abstract class BaseScene(GraphicsDevice graphicsDevice) : IDisposable
{
    protected GraphicsDevice _graphicsDevice = graphicsDevice;
    protected int _currentFrame = 0;
    private bool _disposed = false;

    protected Action? DisposeManaged { get; set; }
    protected Action? DisposeUnmanaged { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                DisposeManaged?.Invoke();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            DisposeUnmanaged?.Invoke();
            _disposed = true;
        }
    }

    public void DrawScene(float width, float height)
    {
        var imageIndex = _graphicsDevice.BeginFrame(_currentFrame);
        if (imageIndex != -1)
        {
            var commandBuffer = _graphicsDevice.GetCommandBuffer(_currentFrame);

            Draw(commandBuffer, imageIndex, width, height);

            _graphicsDevice.EndFrame(commandBuffer, _currentFrame, imageIndex);
            _currentFrame++;
        }
    }

    protected abstract void Draw(CommandBuffer commandBuffer, int imageIndex, float width, float height);
}
