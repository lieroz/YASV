using System.Runtime.InteropServices;
using Silk.NET.Maths;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class ProjectionScene : BaseScene
{
    private static class MathHelper
    {
        public static float DegreesToRadians(float degrees)
        {
            return MathF.PI / 180f * degrees;
        }
    }

    private readonly GraphicsPipelineLayout _projectionGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _projectionGraphicsPipelineDesc;
    private readonly GraphicsPipeline _projectionGraphicsPipeline;
    private readonly VertexBuffer _projectionVertexBuffer;
    private readonly IndexBuffer _projectionIndexBuffer;
    private readonly ConstantBuffer[] _projectionConstantBuffers = new ConstantBuffer[Constants.MaxFramesInFlight];

    private readonly struct Vertex(Vector2D<float> position, Vector3D<float> color)
    {
        private readonly Vector2D<float> _position = position;
        private readonly Vector3D<float> _color = color;

        public readonly Vector2D<float> Position { get => _position; }
        public readonly Vector3D<float> Color { get => _color; }

        public static VertexInputBindingDesc[] BindingDescriptions
        {
            get
            {
                return [
                    new()
                    {
                        Binding = 0,
                        Stride = Marshal.SizeOf<Vertex>(),
                        InputRate = VertexInputRate.Vertex
                    }
                ];
            }
        }

        public static VertexInputAttributeDesc[] AttributeDescriptions
        {
            get
            {
                return [
                    new()
                    {
                        Binding = 0,
                        Location = 0,
                        Format = Format.R32G32_Float,
                        Offset = (int)Marshal.OffsetOf<Vertex>("_position")
                    },
                    new()
                    {
                        Binding = 0,
                        Location = 1,
                        Format = Format.R32G32B32_Float,
                        Offset = (int)Marshal.OffsetOf<Vertex>("_color")
                    }
                ];
            }
        }

        public readonly byte[] Bytes
        {
            get
            {
                var bytes = new byte[Marshal.SizeOf<Vertex>()];
                {
                    var floats = new float[5];
                    _position.CopyTo(floats, 0);
                    _color.CopyTo(floats, 2);
                    System.Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
                }
                return bytes;
            }
        }
    }

    private readonly Vertex[] _vertices =
    [
        new(new(-0.5f, -0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, -0.5f), new(0.0f, 1.0f, 0.0f)),
        new(new(0.5f, 0.5f), new(0.0f, 0.0f, 1.0f)),
        new(new(-0.5f, 0.5f), new(1.0f, 1.0f, 1.0f))
    ];

    // TODO: generate UBO types?
    [StructLayout(LayoutKind.Explicit)]
    private struct UniformBufferObject
    {
        [FieldOffset(0)] private Matrix4X4<float> _model;
        [FieldOffset(64)] private Matrix4X4<float> _view;
        [FieldOffset(128)] private Matrix4X4<float> _projection;

        public Matrix4X4<float> Model { readonly get => _model; set => _model = value; }
        public Matrix4X4<float> View { readonly get => _view; set => _view = value; }
        public Matrix4X4<float> Projection { readonly get => _projection; set => _projection = value; }

        public readonly byte[] Bytes
        {
            get
            {
                // TODO: How to optimize this?
                var bytes = new byte[sizeof(float) * 4 * 4 * 3];
                {
                    var rows = new float[4 * 4 * 3];
                    _model.Row1.CopyTo(rows, 0);
                    _model.Row2.CopyTo(rows, 4);
                    _model.Row3.CopyTo(rows, 8);
                    _model.Row4.CopyTo(rows, 12);
                    _view.Row1.CopyTo(rows, 16);
                    _view.Row2.CopyTo(rows, 20);
                    _view.Row3.CopyTo(rows, 24);
                    _view.Row4.CopyTo(rows, 28);
                    _projection.Row1.CopyTo(rows, 32);
                    _projection.Row2.CopyTo(rows, 36);
                    _projection.Row3.CopyTo(rows, 40);
                    _projection.Row4.CopyTo(rows, 44);
                    System.Buffer.BlockCopy(rows, 0, bytes, 0, bytes.Length);
                }
                return bytes;
            }
        }
    }

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
                    ShaderStages = [ShaderStage.Vertex],
                    Samplers = null
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

    protected override void Draw(CommandBuffer commandBuffer, int imageIndex, float width, float height)
    {
        _graphicsDevice.BeginCommandBuffer(commandBuffer);
        {
            _graphicsDevice.ImageBarrier(commandBuffer, imageIndex, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal);

            _graphicsDevice.BeginRendering(commandBuffer, imageIndex);
            {
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _projectionGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(90.0f)),
                    View = Matrix4X4.CreateLookAt<float>(new(2.0f, 2.0f, 2.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 1.0f)),
                    Projection = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), width / height, 0.1f, 10.0f)
                };

                var frameIndex = _currentFrame % Constants.MaxFramesInFlight;

                var cb = _projectionConstantBuffers[frameIndex];
                _graphicsDevice.CopyDataToConstantBuffer(cb, ubo.Bytes);

                var descriptorWriter = _graphicsDevice.GetDescriptorWriter();
                var descriptorSet = _graphicsDevice.GetDescriptorSet(frameIndex, _projectionGraphicsPipelineLayout);
                _graphicsDevice.BindConstantBuffer(descriptorWriter, 0, cb, cb.Size, 0, DescriptorType.UniformBuffer, descriptorSet);
                _graphicsDevice.BindDescriptorSet(commandBuffer, _projectionGraphicsPipelineLayout, descriptorSet);
                // _graphicsDevice.UpdateDescriptorSet(descriptorWriter);

                _graphicsDevice.SetDefaultViewportAndScissor(commandBuffer);
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_projectionVertexBuffer]);
                _graphicsDevice.BindIndexBuffer(commandBuffer, _projectionIndexBuffer, IndexType.Uint16);
                _graphicsDevice.DrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, imageIndex, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
