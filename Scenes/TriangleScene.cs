using System.Runtime.InteropServices;
using Silk.NET.Maths;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class TriangleScene : BaseScene
{
    private readonly GraphicsPipelineLayout _triangleGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _triangleGraphicsPipelineDesc;
    private readonly GraphicsPipeline _triangleGraphicsPipeline;
    private readonly YASV.RHI.Buffer _triangleVertexBuffer;

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

        public byte[] Bytes
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
        new(new(0.0f, -0.5f), new(1.0f, 0.0f, 0.0f)),
        new(new(0.5f, 0.5f), new(0.0f, 1.0f, 0.0f)),
        new(new(-0.5f, 0.5f), new(0.0f, 0.0f, 1.0f))
    ];

    public TriangleScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/triangle.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/triangle.frag.hlsl", ShaderStage.Pixel);

        _triangleGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
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

        _triangleGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
        {
            SetLayoutCount = 0,
            SetLayouts = null,
            PushConstantRangeCount = 0,
            PushConstantRanges = null
        });
        _triangleGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_triangleGraphicsPipelineDesc, _triangleGraphicsPipelineLayout);

        _triangleVertexBuffer = _graphicsDevice.CreateBuffer(new()
        {
            Size = Marshal.SizeOf<Vertex>() * _vertices.Length,
            Usage = BufferUsage.Vertex,
            SharingMode = SharingMode.Exclusive
        });

        var data = new byte[Marshal.SizeOf<Vertex>() * _vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            System.Buffer.BlockCopy(_vertices[i].Bytes, 0, data, Marshal.SizeOf<Vertex>() * i, Marshal.SizeOf<Vertex>());
        }
        _graphicsDevice.CopyToBuffer(_triangleVertexBuffer, data);

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_triangleGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_triangleGraphicsPipelineLayout]);
            _graphicsDevice.DestroyBuffer(_triangleVertexBuffer);
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
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_triangleVertexBuffer]);
                _graphicsDevice.Draw(commandBuffer, (uint)_vertices.Length, 1, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, imageIndex, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
