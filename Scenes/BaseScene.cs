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

    public void DrawScene()
    {
        Draw();
    }

    protected abstract void Draw();
}
