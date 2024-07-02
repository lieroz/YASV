using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class TriangleScene : BaseScene
{
    private readonly GraphicsPipelineLayout _triangleGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _triangleGraphicsPipelineDesc;
    private readonly GraphicsPipeline _triangleGraphicsPipeline;

    public TriangleScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        var vertexShader = _graphicsDevice.CreateShader("Shaders/triangle.vert.hlsl", Shader.Stage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/triangle.frag.hlsl", Shader.Stage.Fragment);
        _triangleGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
            .SetVertexShader(vertexShader)
            .SetFragmentShader(fragmentShader)
            .SetVertexInputState(default)
            .SetInputAssemblyState(new() { PrimitiveTopology = PrimitiveTopology.TriangleList })
            .SetRasterizationState(new()
            {
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = false,
                DepthBiasConstantFactor = 0.0f,
                DepthBiasClamp = 0.0f,
                DepthBiasSlopeFactor = 0.0f
            })
            .SetMultisampleState(new()
            {
                SampleShadingEnable = false,
                SampleCountFlags = SampleCountFlags.Count1Bit,
                MinSampleShading = 1.0f,
                SampleMask = null,
                AlphaCoverageEnable = false,
                AlphaToOneEnable = false
            })
            .SetColorBlendAttachmentState(0, new()
            {
                ColorComponentFlags = [ColorComponentFlags.RBit, ColorComponentFlags.GBit, ColorComponentFlags.BBit, ColorComponentFlags.ABit],
                BlendEnable = false,
                SrcColorBlendFactor = BlendFactor.One,
                DstColorBlendFactor = BlendFactor.Zero,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
                AlphaBlendOp = BlendOp.Add
            })
            .SetColorBlendState(new()
            {
                LogicOpEnable = false,
                LogicOp = LogicOp.Clear,
                AttachmentCount = 1,
                BlendConstants = [0.0f, 0.0f, 0.0f, 0.0f]
            })
            .Build();

        _triangleGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
        {
            SetLayoutCount = 0,
            SetLayouts = null,
            PushConstantRangeCount = 0,
            PushConstantRanges = null
        });
        _triangleGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_triangleGraphicsPipelineDesc, _triangleGraphicsPipelineLayout);

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_triangleGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_triangleGraphicsPipelineLayout]);
        };
    }

    protected override void Draw(ICommandBuffer commandBuffer, int imageIndex)
    {
        _graphicsDevice.BeginCommandBuffer(commandBuffer);
        {
            _graphicsDevice.ImageBarrier(commandBuffer, imageIndex, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal);

            _graphicsDevice.BeginRendering(commandBuffer, imageIndex);
            {
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _triangleGraphicsPipeline);

                _graphicsDevice.SetDefaultViewportAndScissor(commandBuffer);
                _graphicsDevice.Draw(commandBuffer, 3, 1, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, imageIndex, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
