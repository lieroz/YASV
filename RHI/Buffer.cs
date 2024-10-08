using System.Collections.Concurrent;

namespace YASV.RHI;

public class Buffer(int size)
{
    public int Size { get; } = size;
}

public class VertexBuffer(int size) : Buffer(size)
{
}

public class IndexBuffer(int size) : Buffer(size)
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

    public void ReturnStagingBuffer(StagingBuffer buffer, Action<StagingBuffer> action)
    {
        if (!_pools.TryGetValue(buffer.Size, out var buffers))
        {
            var stack = new ConcurrentStack<StagingBuffer>();
            stack.Push(buffer);

            int retries = 0;
            for (; retries < 3 && !_pools.TryAdd(buffer.Size, stack); retries++) ;

            if (retries == 3)
            {
                action(buffer);
            }
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
