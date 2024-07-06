using System;
using System.Collections.Concurrent;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public interface ICommandBuffer { }

public interface ITexture { }

// TODO: rearrange
public abstract class GraphicsDevice(IView view)
{
    private const int PreallocatedBuffersCount = 3;

    protected readonly IView _view = view;

    public abstract void Create(Sdl sdlApi);

    public abstract void Destroy();

    public abstract void WaitIdle();

    public abstract int BeginFrame(int currentFrame);
    public abstract void EndFrame(ICommandBuffer commandBuffer, int currentFrame, int imageIndex);

    // TODO: generalize this, add more options
    public abstract void ImageBarrier(ICommandBuffer commandBuffer, int imageIndex, ImageLayout oldLayout, ImageLayout newLayout);

    public abstract void BeginRendering(ICommandBuffer commandBuffer, int imageIndex);
    public abstract void EndRendering(ICommandBuffer commandBuffer);

    private readonly ConcurrentBag<ICommandBuffer>[] _commandBuffers = [[], []];

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

    public abstract Shader CreateShader(string path, Shader.Stage stage);
    public abstract unsafe void DestroyShaders(Shader[] shaders);
}
