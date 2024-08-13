using System.Runtime.InteropServices;
using Silk.NET.Maths;
using YASV.Helpers;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class ProjectionScene : BaseScene
{
    private readonly GraphicsPipelineLayout _projectionGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _projectionGraphicsPipelineDesc;
    private readonly GraphicsPipeline _projectionGraphicsPipeline;
    private readonly VertexBuffer _projectionVertexBuffer;
    private readonly IndexBuffer _projectionIndexBuffer;
    private readonly ConstantBuffer[] _projectionConstantBuffers = new ConstantBuffer[Constants.MaxFramesInFlight];

    private readonly Vertex[] _vertices =
    [
        new(new(-0.5f, -0.5f, 0.0f), new(1.0f, 0.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.0f), new(0.0f, 0.0f, 1.0f), new(0.0f, 0.0f)),
        new(new(-0.5f, 0.5f, 0.0f), new(1.0f, 1.0f, 1.0f), new(0.0f, 0.0f))
    ];

    private readonly short[] _indices = [0, 1, 2, 2, 3, 0];

    public ProjectionScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/projection.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/triangle.frag.hlsl", ShaderStage.Pixel);

        _projectionGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
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

        _projectionGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
        {
            SetLayouts = [new() { Bindings = [
                new() {
                    Binding = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    ShaderStages = [ShaderStage.Vertex]
                }]
            }],
            PushConstantRanges = null
        });
        _projectionGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_projectionGraphicsPipelineDesc, _projectionGraphicsPipelineLayout);

        int vertexBufferSize = Marshal.SizeOf<Vertex>() * _vertices.Length;
        _projectionVertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

        int indexBufferSize = sizeof(short) * _indices.Length;
        _projectionIndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

        var vertexData = new byte[Marshal.SizeOf<Vertex>() * _vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            System.Buffer.BlockCopy(_vertices[i].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * i, Marshal.SizeOf<Vertex>());
        }

        var indexData = new byte[sizeof(short) * _indices.Length];
        System.Buffer.BlockCopy(_indices, 0, indexData, 0, indexData.Length);

        _graphicsDevice.CopyDataToVertexBuffer(_projectionVertexBuffer, vertexData);
        _graphicsDevice.CopyDataToIndexBuffer(_projectionIndexBuffer, indexData);

        for (int i = 0; i < _projectionConstantBuffers.Length; i++)
        {
            _projectionConstantBuffers[i] = graphicsDevice.CreateConstantBuffer(Marshal.SizeOf<UniformBufferObject>());
        }

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_projectionGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_projectionGraphicsPipelineLayout]);
            _graphicsDevice.DestroyVertexBuffer(_projectionVertexBuffer);
            _graphicsDevice.DestroyIndexBuffer(_projectionIndexBuffer);

            foreach (var constantBuffer in _projectionConstantBuffers)
            {
                _graphicsDevice.DestroyConstantBuffer(constantBuffer);
            }
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
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _projectionGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4.CreateRotationZ(MathHelpers.DegreesToRadians(90.0f)),
                    View = Matrix4X4.CreateLookAt<float>(new(2.0f, 2.0f, 2.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 1.0f)),
                    Projection = Matrix4X4.CreatePerspectiveFieldOfView(MathHelpers.DegreesToRadians(45.0f), width / height, 0.1f, 10.0f)
                };

                var frameIndex = _currentFrame % Constants.MaxFramesInFlight;

                var cb = _projectionConstantBuffers[frameIndex];
                _graphicsDevice.CopyDataToConstantBuffer(cb, ubo.Bytes);

                var descriptorWriter = _graphicsDevice.GetDescriptorWriter();
                var descriptorSet = _graphicsDevice.GetDescriptorSet(frameIndex, _projectionGraphicsPipelineLayout);
                _graphicsDevice.BindConstantBuffer(descriptorWriter, 0, cb, cb.Size, 0, DescriptorType.UniformBuffer);
                _graphicsDevice.UpdateDescriptorSet(descriptorWriter, descriptorSet);
                _graphicsDevice.BindDescriptorSet(commandBuffer, _projectionGraphicsPipelineLayout, descriptorSet);

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
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_projectionVertexBuffer]);
                _graphicsDevice.BindIndexBuffer(commandBuffer, _projectionIndexBuffer, IndexType.Uint16);
                _graphicsDevice.DrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
