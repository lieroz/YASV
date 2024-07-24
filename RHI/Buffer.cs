using System.Collections.Concurrent;

namespace YASV.RHI;

public class Buffer(int size)
{
    public int Size { get; } = size;
}

public class DeviceLocalBuffer(int size) : Buffer(size)
{
}

public class VertexBuffer(int size) : DeviceLocalBuffer(size)
{
}

public class IndexBuffer(int size) : DeviceLocalBuffer(size)
{
}

public class StagingBuffer(int size) : Buffer(size)
{
}

public class ConstantBuffer(int size) : Buffer(size)
{
}

public class StagingBufferPool
{
    private readonly ConcurrentDictionary<int, ConcurrentStack<StagingBuffer>> _pools = [];

    public StagingBuffer? GetStagingBuffer(int size)
    {
        if (_pools.TryGetValue(size, out var pool))
        {
            if (pool.TryPop(out var buffer))
            {
                return buffer;
            }
        }
        return null;
    }

    public void ReturnStagingBuffer(StagingBuffer buffer)
    {
        if (!_pools.TryGetValue(buffer.Size, out var buffers))
        {
            var stack = new ConcurrentStack<StagingBuffer>();
            stack.Push(buffer);

            // TODO: check for infinite loop
            while (!_pools.TryAdd(buffer.Size, stack)) ;
        }
        else
        {
            buffers.Push(buffer);
        }
    }

    public void Clear(Action<StagingBuffer> action)
    {
        foreach (var pool in _pools)
        {
            foreach (var buffer in pool.Value)
            {
                action(buffer);
            }
        }
    }
}
