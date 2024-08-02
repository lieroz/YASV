using System.Collections.Concurrent;

namespace YASV.RHI;

public class CommandBuffer
{
}

public class CommandBufferPool
{
    private readonly ConcurrentBag<CommandBuffer>[] _freeCommandBuffers = new ConcurrentBag<CommandBuffer>[Constants.MaxFramesInFlight];
    private readonly ConcurrentBag<CommandBuffer>[] _inUseCommandBuffers = new ConcurrentBag<CommandBuffer>[Constants.MaxFramesInFlight];

    public CommandBufferPool()
    {
        for (int i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _freeCommandBuffers[i] = [];
            _inUseCommandBuffers[i] = [];
        }
    }

    public CommandBuffer GetCommandBuffer(Func<CommandBuffer[]> allocateAction, int frameIndex)
    {
        var bag = _freeCommandBuffers[frameIndex];
        CommandBuffer? commandBuffer;

        while (!bag.TryTake(out commandBuffer))
        {
            var allocatedBuffers = allocateAction();
            foreach (var buffer in allocatedBuffers)
            {
                bag.Add(buffer);
            }
        }

        _inUseCommandBuffers[frameIndex].Add(commandBuffer);
        return commandBuffer;
    }

    public void Reset(int frameIndex)
    {
        var freeBag = _freeCommandBuffers[frameIndex];
        var inUseBag = _inUseCommandBuffers[frameIndex];

        foreach (var commandBuffer in inUseBag.Take(inUseBag.Count))
        {
            freeBag.Add(commandBuffer);
        }
        inUseBag.Clear();
    }
}
