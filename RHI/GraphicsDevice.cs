using System.Numerics;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public abstract class GraphicsDevice(IView view)
{
    protected readonly IView _view = view;
    private readonly CommandBufferPool _commandBufferPool = new();
    private readonly StagingBufferPool _stagingBufferPool = new();

    public abstract void Create(Sdl sdlApi);
    public void Destroy()
    {
        _stagingBufferPool.Clear(DestroyStagingBuffer);
        DestroyInternal();
    }
    protected abstract void DestroyInternal();

    public int BeginFrame(int frameNumber)
    {
        int frameIndex = frameNumber % Constants.MaxFramesInFlight;
        int imageIndex = BeginFrameInternal(frameIndex);

        ResetCommandBuffers(frameIndex);
        _commandBufferPool.Reset(frameIndex);

        return imageIndex;
    }
    public abstract int BeginFrameInternal(int frameIndex);

    public void EndFrame(CommandBuffer commandBuffer, int frameNumber, int imageIndex)
    {
        int frameIndex = frameNumber % Constants.MaxFramesInFlight;
        EndFrameInternal(commandBuffer, frameIndex, imageIndex);
    }
    public abstract void EndFrameInternal(CommandBuffer commandBuffer, int frameIndex, int imageIndex);

    public abstract void WaitIdle();

    public CommandBuffer GetCommandBuffer(int frameNumber)
    {
        int frameIndex = frameNumber % Constants.MaxFramesInFlight;
        return _commandBufferPool.GetCommandBuffer(() => AllocateCommandBuffers(frameIndex, Constants.PreallocatedBuffersCount), frameIndex);
    }

    protected abstract CommandBuffer[] AllocateCommandBuffers(int index, int count);
    protected abstract void ResetCommandBuffers(int index);

    protected StagingBuffer GetStagingBuffer(int size)
    {
        var roundedSize = (int)BitOperations.RoundUpToPowerOf2((uint)size);
        return _stagingBufferPool.GetStagingBuffer(roundedSize) ?? CreateStagingBuffer(roundedSize);
    }

    protected void ReturnStagingBuffer(StagingBuffer stagingBuffer)
    {
        _stagingBufferPool.ReturnStagingBuffer(stagingBuffer, DestroyStagingBuffer);
    }

    // TODO: generalize this, add more options
    public abstract void ImageBarrier(CommandBuffer commandBuffer, int imageIndex, ImageLayout oldLayout, ImageLayout newLayout);

    public abstract void BeginRendering(CommandBuffer commandBuffer, int imageIndex);
    public abstract void EndRendering(CommandBuffer commandBuffer);

    public abstract void BeginCommandBuffer(CommandBuffer commandBuffer);
    public abstract void EndCommandBuffer(CommandBuffer commandBuffer);

    public abstract Shader CreateShader(string path, ShaderStage stage);
    public abstract unsafe void DestroyShaders(Shader[] shaders);

    public abstract DescriptorWriter GetDescriptorWriter();
    public abstract DescriptorSet GetDescriptorSet(int frameIndex, GraphicsPipelineLayout layout);
    public abstract void BindConstantBuffer(DescriptorWriter writer, int binding, ConstantBuffer buffer, int size, int offset, DescriptorType type, DescriptorSet descriptorSet);
    public abstract void UpdateDescriptorSet(DescriptorWriter writer);
    public abstract void BindDescriptorSet(CommandBuffer commandBuffer, GraphicsPipelineLayout layout, DescriptorSet set);

    public abstract GraphicsPipelineLayout CreateGraphicsPipelineLayout(GraphicsPipelineLayoutDesc desc);
    public abstract void DestroyGraphicsPipelineLayouts(GraphicsPipelineLayout[] layouts);

    public abstract GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc, GraphicsPipelineLayout layout);
    public abstract void DestroyGraphicsPipelines(GraphicsPipeline[] pipelines);
    public abstract void BindGraphicsPipeline(CommandBuffer commandBuffer, GraphicsPipeline pipeline);

    public abstract void SetDefaultViewportAndScissor(CommandBuffer commandBuffer);
    public abstract void SetViewports(CommandBuffer commandBuffer, int firstViewport, Viewport[] viewports);
    public abstract void SetScissors(CommandBuffer commandBuffer, int firstScissor, Rect2D[] scissors);

    public abstract void Draw(CommandBuffer commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
    public abstract void DrawIndexed(CommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    public abstract VertexBuffer CreateVertexBuffer(int size);
    public abstract IndexBuffer CreateIndexBuffer(int size);
    public abstract ConstantBuffer CreateConstantBuffer(int size);
    protected abstract StagingBuffer CreateStagingBuffer(int size);

    public abstract void DestroyVertexBuffer(VertexBuffer buffer);
    public abstract void DestroyIndexBuffer(IndexBuffer buffer);
    public abstract void DestroyConstantBuffer(ConstantBuffer buffer);
    protected abstract void DestroyStagingBuffer(StagingBuffer buffer);

    public abstract void CopyDataToVertexBuffer(VertexBuffer buffer, byte[] data);
    public abstract void CopyDataToIndexBuffer(IndexBuffer buffer, byte[] data);
    public abstract void CopyDataToConstantBuffer(ConstantBuffer buffer, byte[] data);

    // TODO: Add offsets
    public abstract void BindVertexBuffers(CommandBuffer commandBuffer, VertexBuffer[] buffers);
    public abstract void BindIndexBuffer(CommandBuffer commandBuffer, IndexBuffer buffer, IndexType indexType);
}
