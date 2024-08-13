using System.Runtime.InteropServices;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class RectangleScene : BaseScene
{
    private readonly GraphicsPipelineLayout _rectangleGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _rectangleGraphicsPipelineDesc;
    private readonly GraphicsPipeline _rectangleGraphicsPipeline;
    private readonly VertexBuffer _rectangleVertexBuffer;
    private readonly IndexBuffer _rectangleIndexBuffer;

    private readonly Vertex[] _vertices =
    [
        new(new(-0.5f, -0.5f, 0.0f), new(1.0f, 0.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.0f), new(0.0f, 0.0f, 1.0f), new(0.0f, 0.0f)),
        new(new(-0.5f, 0.5f, 0.0f), new(1.0f, 1.0f, 1.0f), new(0.0f, 0.0f))
    ];

    private readonly short[] _indices = [0, 1, 2, 2, 3, 0];

    public RectangleScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/triangle.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/triangle.frag.hlsl", ShaderStage.Pixel);

        _rectangleGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
            .SetVertexShader(vertexShader)
            .SetPixelShader(fragmentShader)
            .SetVertexInputState(new()
            {
                BindingDescriptions = Vertex.BindingDescriptions,
                AttributeDescriptions = Vertex.AttributeDescriptions
            })
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

        _rectangleGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
        {
            SetLayouts = null,
            PushConstantRanges = null
        });
        _rectangleGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_rectangleGraphicsPipelineDesc, _rectangleGraphicsPipelineLayout);

        int vertexBufferSize = Marshal.SizeOf<Vertex>() * _vertices.Length;
        _rectangleVertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

        int indexBufferSize = sizeof(short) * _indices.Length;
        _rectangleIndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

        var vertexData = new byte[Marshal.SizeOf<Vertex>() * _vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            System.Buffer.BlockCopy(_vertices[i].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * i, Marshal.SizeOf<Vertex>());
        }

        var indexData = new byte[sizeof(short) * _indices.Length];
        System.Buffer.BlockCopy(_indices, 0, indexData, 0, indexData.Length);

        _graphicsDevice.CopyDataToVertexBuffer(_rectangleVertexBuffer, vertexData);
        _graphicsDevice.CopyDataToIndexBuffer(_rectangleIndexBuffer, indexData);

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_rectangleGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_rectangleGraphicsPipelineLayout]);
            _graphicsDevice.DestroyVertexBuffer(_rectangleVertexBuffer);
            _graphicsDevice.DestroyIndexBuffer(_rectangleIndexBuffer);
        };
    }

    protected override void Draw(CommandBuffer commandBuffer, int imageIndex)
    {
        var backBuffer = _graphicsDevice.GetBackBuffer(imageIndex);
        var (width, height) = _graphicsDevice.GetSwapchainSizes();

        _graphicsDevice.BeginCommandBuffer(commandBuffer);
        {
            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal);

            _graphicsDevice.BeginRendering(commandBuffer, backBuffer);
            {
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _rectangleGraphicsPipeline);

                _graphicsDevice.SetViewports(commandBuffer, 0, [
                    new()
                    {
                        X = 0.0f, Y = 0.0f, Width = width, Height = height, MinDepth = 0.0f, MaxDepth = 1.0f
                    }
                ]);
                _graphicsDevice.SetScissors(commandBuffer, 0, [
                    new()
                    {
                        X = 0, Y = 0, Width = (int)width, Height = (int)height
                    }
                ]);
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_rectangleVertexBuffer]);
                _graphicsDevice.BindIndexBuffer(commandBuffer, _rectangleIndexBuffer, IndexType.Uint16);
                _graphicsDevice.DrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
