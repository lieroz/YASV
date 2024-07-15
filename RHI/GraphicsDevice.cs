using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public interface ICommandBuffer { }

// TODO: rearrange
public abstract class GraphicsDevice
{
    protected const int PreallocatedBuffersCount = 3;

    protected readonly IView _view;

    public abstract void Create(Sdl sdlApi);

    public void Destroy()
    {
        foreach (var pool in _bufferPool)
        {
            foreach (var buffer in pool.Value)
            {
                DestroyBuffer(buffer);
            }
        }

        DestroyInternal();
    }

    protected abstract void DestroyInternal();

    public abstract void WaitIdle();

    public abstract int BeginFrame(int currentFrame);
    public abstract void EndFrame(ICommandBuffer commandBuffer, int currentFrame, int imageIndex);

    public GraphicsDevice(IView view)
    {
        _view = view;
        for (int i = 0; i < _commandBuffers.Length; i++)
        {
            _commandBuffers[i] = [];
        }
    }

    private readonly ConcurrentBag<ICommandBuffer>[] _commandBuffers = new ConcurrentBag<ICommandBuffer>[Constants.MaxFramesInFlight];

    public ICommandBuffer GetCommandBuffer(int frameNumber)
    {
        int index = frameNumber % Constants.MaxFramesInFlight;
        var bag = _commandBuffers[index];

        ResetCommandBuffers(index);

        ICommandBuffer? commandBuffer;

        while (!bag.TryTake(out commandBuffer))
        {
            foreach (var buffer in AllocateCommandBuffers(index, PreallocatedBuffersCount))
            {
                bag.Add(buffer);
            }
        }

        return commandBuffer;
    }

    protected abstract ICommandBuffer[] AllocateCommandBuffers(int index, int count);
    protected abstract void ResetCommandBuffers(int index);

    private readonly ConcurrentDictionary<int, ConcurrentStack<Buffer>> _bufferPool = [];

    protected Buffer GetStagingBuffer(int size)
    {
        var poolSize = (int)BitOperations.RoundUpToPowerOf2((uint)size);
        if (_bufferPool.TryGetValue(poolSize, out var buffers))
        {
            if (buffers.TryPop(out var buffer))
            {
                return buffer;
            }
        }

        return CreateStagingBuffer(new()
        {
            Size = poolSize,
            Usages = [BufferUsage.TransferSrc],
            SharingMode = SharingMode.Exclusive
        });
    }

    protected void ReturnStagingBuffer(Buffer buffer)
    {
        if (!_bufferPool.TryGetValue(buffer.Size, out var buffers))
        {
            var stack = new ConcurrentStack<Buffer>();
            stack.Push(buffer);

            // TODO: check for infinite loop
            while (!_bufferPool.TryAdd(buffer.Size, stack)) ;
        }
        else
        {
            buffers.Push(buffer);
        }
    }

    // TODO: generalize this, add more options
    public abstract void ImageBarrier(ICommandBuffer commandBuffer, int imageIndex, ImageLayout oldLayout, ImageLayout newLayout);

    public abstract void BeginRendering(ICommandBuffer commandBuffer, int imageIndex);
    public abstract void EndRendering(ICommandBuffer commandBuffer);

    public abstract void BeginCommandBuffer(ICommandBuffer commandBuffer);
    public abstract void EndCommandBuffer(ICommandBuffer commandBuffer);

    public abstract void SetDefaultViewportAndScissor(ICommandBuffer commandBuffer);
    public abstract void SetViewports(ICommandBuffer commandBuffer, int firstViewport, Viewport[] viewports);
    public abstract void SetScissors(ICommandBuffer commandBuffer, int firstScissor, Rect2D[] scissors);
    public abstract void Draw(ICommandBuffer commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
    public abstract void DrawIndexed(ICommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    public abstract GraphicsPipelineLayout CreateGraphicsPipelineLayout(GraphicsPipelineLayoutDesc desc);
    public abstract void DestroyGraphicsPipelineLayouts(GraphicsPipelineLayout[] layouts);

    public abstract GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, GraphicsPipelineLayout layout);
    public abstract void DestroyGraphicsPipelines(GraphicsPipeline[] pipelines);
    public abstract void BindGraphicsPipeline(ICommandBuffer commandBuffer, GraphicsPipeline pipeline);

    public abstract Shader CreateShader(string path, ShaderStage stage);
    public abstract unsafe void DestroyShaders(Shader[] shaders);

    public Buffer CreateVertexBuffer(BufferDesc desc)
    {
        if (!desc.Usages.Contains(BufferUsage.Vertex))
        {
            throw new ArgumentException("Vertex buffer must container BufferUsage.Vertex bit!");
        }
        return CreateVertexBufferInternal(desc);
    }

    public Buffer CreateIndexBuffer(BufferDesc desc)
    {
        if (!desc.Usages.Contains(BufferUsage.Index))
        {
            throw new ArgumentException("Index buffer must container BufferUsage.Index bit!");
        }
        return CreateIndexBufferInternal(desc);
    }

    public abstract Buffer CreateStagingBuffer(BufferDesc desc);

    protected abstract Buffer CreateVertexBufferInternal(BufferDesc desc);
    protected abstract Buffer CreateIndexBufferInternal(BufferDesc desc);

    public abstract void DestroyBuffer(Buffer buffer);
    public abstract unsafe void CopyDataToBuffer(Buffer buffer, byte[] data, int currentFrame);
    // TODO: Add offsets
    public abstract void BindVertexBuffers(ICommandBuffer commandBuffer, Buffer[] buffers);
    public abstract void BindIndexBuffer(ICommandBuffer commandBuffer, Buffer buffer, IndexType indexType);
}
