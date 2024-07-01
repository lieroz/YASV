using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class TriangleScene : BaseScene
{
    // TODO: Add graphics object destruction
    private readonly GraphicsPipelineLayout _triangleGraphicsPipelineLayout;
    private readonly GraphicsPipeline _triangleGraphicsPipeline;

    public TriangleScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        var vertexShader = _graphicsDevice.CreateShader("Shaders/triangle.vert.hlsl", Shader.Stage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/triangle.frag.hlsl", Shader.Stage.Fragment);
        _triangleGraphicsPipelineLayout = new GraphicsPipelineLayoutBuilder()
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
            .SetRenderTarget0(new()
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

        _triangleGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_triangleGraphicsPipelineLayout);

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);
    }

    public override void Draw()
    {
        // var commandBuffer = _graphicsDevice.GetCommandBuffer(_currentFrame);
        // _graphicsDevice.BeginCommandBuffer(commandBuffer);
        // {
        //     {
        //         _graphicsDevice.BindGraphicsPipeline(commandBuffer, _triangleGraphicsPipeline);
        //     }
        // }
        // _graphicsDevice.EndCommandBuffer(commandBuffer);
        _currentFrame++;
    }
}
