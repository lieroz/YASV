using System.Collections.Concurrent;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace YASV.RHI;

public interface ICommandBuffer { }

public interface ITexture { }

public abstract class GraphicsDevice(IView view)
{
    protected readonly IView _view = view;

    public abstract void Create(Sdl sdlApi);

    public abstract void Destroy();

    public abstract void WaitIdle();

    public abstract void DrawFrame();

    private const int PreallocatedBuffersCount = 3;

    private ConcurrentBag<ICommandBuffer>[] _commandBuffers = new ConcurrentBag<ICommandBuffer>[Constants.MaxFramesInFlight];

    public ICommandBuffer GetCommandBuffer(int frameNumber)
    {
        int index = frameNumber % Constants.MaxFramesInFlight;
        var bag = _commandBuffers[index];

        foreach (var buffer in bag)
        {
            ResetCommandBuffer(buffer);
        }

        ICommandBuffer? commandBuffer;

        while (!bag.TryTake(out commandBuffer))
        {
            foreach (var buffer in AllocateCommandBuffers(PreallocatedBuffersCount))
            {
                bag.Add(buffer);
            }
        }

        return commandBuffer;
    }

    protected abstract ICommandBuffer[] AllocateCommandBuffers(int count);
    protected abstract void ResetCommandBuffer(ICommandBuffer commandBuffer);

    public abstract void BeginCommandBuffer(ICommandBuffer commandBuffer);
    public abstract void EndCommandBuffer(ICommandBuffer commandBuffer);

    public abstract void SetViewport(ICommandBuffer commandBuffer, int x, int y, int width, int height, float minDepth = 0.0f, float maxDepth = 1.0f);
    public abstract void SetScissor(ICommandBuffer commandBuffer, int x, int y, int width, int height);
    public abstract void Draw(ICommandBuffer commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
    public abstract void DrawIndexed(ICommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    // TODO: Add render pass abstraction
    public abstract void BeginRenderPass(ICommandBuffer commandBuffer);
    public abstract void EndRenderPass(ICommandBuffer commandBuffer);

    // TODO: Add graphics pipeline abstraction
    public abstract GraphicsPipeline CreateGraphicsPipeline(GraphicsPipelineLayout layout);
    public abstract void BindGraphicsPipeline(ICommandBuffer commandBuffer, GraphicsPipeline pipeline);

    public abstract Shader CreateShader(string path, Shader.Stage stage);
    public abstract unsafe void DestroyShaders(Shader[] shaders);
}
