using System.Runtime.InteropServices;
using Silk.NET.Maths;
using SkiaSharp;
using YASV.GraphicsEntities;
using YASV.Helpers;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class DepthBuffering : BaseScene
{
    private readonly GraphicsPipelineLayout _depthBufferingGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _depthBufferingGraphicsPipelineDesc;
    private readonly GraphicsPipeline _depthBufferingGraphicsPipeline;
    private readonly VertexBuffer _depthBufferingVertexBuffer;
    private readonly IndexBuffer _depthBufferingIndexBuffer;
    private readonly ConstantBuffer[] _depthBufferingConstantBuffers = new ConstantBuffer[Constants.MaxFramesInFlight];
    private readonly Texture _texture;
    private Texture _depthTexture;
    private readonly TextureSampler _textureSampler;

    private readonly Vertex[] _vertices =
    [
        new(new(-0.5f, -0.5f, 0.0f), new(1.0f, 0.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f), new(1.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.0f), new(0.0f, 0.0f, 1.0f), new(1.0f, 1.0f)),
        new(new(-0.5f, 0.5f, 0.0f), new(1.0f, 1.0f, 1.0f), new(0.0f, 1.0f)),

        new(new(-0.5f, -0.5f, -0.5f), new(1.0f, 0.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, -0.5f, -0.5f), new(0.0f, 1.0f, 0.0f), new(1.0f, 0.0f)),
        new(new(0.5f, 0.5f, -0.5f), new(0.0f, 0.0f, 1.0f), new(1.0f, 1.0f)),
        new(new(-0.5f, 0.5f, -0.5f), new(1.0f, 1.0f, 1.0f), new(0.0f, 1.0f))
    ];

    private readonly short[] _indices = [
        0, 1, 2, 2, 3, 0,
        4, 5, 6, 6, 7, 4
    ];

    public DepthBuffering(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/textureMapping.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/textureMapping.frag.hlsl", ShaderStage.Pixel);

        _depthBufferingGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
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
            .SetDepthStencilState(new()
            {
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = false,
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f,
                StencilTestEnable = false
            })
            .Build();

        _depthBufferingGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
        {
            SetLayouts = [new() { Bindings = [
                new() {
                    Binding = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    ShaderStages = [ShaderStage.Vertex]
                },
                new() {
                    Binding = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    DescriptorCount = 1,
                    ShaderStages = [ShaderStage.Pixel]
                }]
            }],
            PushConstantRanges = null
        });
        _depthBufferingGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_depthBufferingGraphicsPipelineDesc, _depthBufferingGraphicsPipelineLayout);

        int vertexBufferSize = Marshal.SizeOf<Vertex>() * _vertices.Length;
        _depthBufferingVertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

        int indexBufferSize = sizeof(short) * _indices.Length;
        _depthBufferingIndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

        var vertexData = new byte[Marshal.SizeOf<Vertex>() * _vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            System.Buffer.BlockCopy(_vertices[i].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * i, Marshal.SizeOf<Vertex>());
        }

        var indexData = new byte[sizeof(short) * _indices.Length];
        System.Buffer.BlockCopy(_indices, 0, indexData, 0, indexData.Length);

        _graphicsDevice.CopyDataToVertexBuffer(_depthBufferingVertexBuffer, vertexData);
        _graphicsDevice.CopyDataToIndexBuffer(_depthBufferingIndexBuffer, indexData);

        for (int i = 0; i < _depthBufferingConstantBuffers.Length; i++)
        {
            _depthBufferingConstantBuffers[i] = graphicsDevice.CreateConstantBuffer(Marshal.SizeOf<UniformBufferObject>());
        }

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_depthBufferingGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_depthBufferingGraphicsPipelineLayout]);
            _graphicsDevice.DestroyVertexBuffer(_depthBufferingVertexBuffer);
            _graphicsDevice.DestroyIndexBuffer(_depthBufferingIndexBuffer);

            foreach (var constantBuffer in _depthBufferingConstantBuffers)
            {
                _graphicsDevice.DestroyConstantBuffer(constantBuffer);
            }

            _graphicsDevice.DestoryTexture(_texture!);
            _graphicsDevice.DestoryTexture(_depthTexture!);
            _graphicsDevice.DestroyTextureSampler(_textureSampler!);
        };
        DisposeManaged += () =>
        {
            _graphicsDevice.RecreateTexturesAction = null;
        };


        var data = File.ReadAllBytes("Assets/texture.jpg");
        var image = SKImage.FromEncodedData(data);

        _texture = _graphicsDevice.CreateTextureFromImage(image);
        _textureSampler = _graphicsDevice.CreateTextureSampler(
            new()
            {
                MagFilter = Filter.Linear,
                MinFilter = Filter.Linear,
                AddressModeU = SamplerAddressMode.Repeat,
                AddressModeV = SamplerAddressMode.Repeat,
                AddressModeW = SamplerAddressMode.Repeat,
                AnisotropyEnable = true,
                BorderColor = BorderColor.IntOpaqueBlack,
                UnnormalizedCoordinates = false,
                CompareEnable = false,
                CompareOp = CompareOp.Always,
                MipmapMode = SamplerMipmapMode.Linear,
                MipLodBias = 0,
                MinLod = 0,
                MaxLod = 0
            }
        );

        var (width, height) = _graphicsDevice.GetSwapchainSizes();
        _depthTexture = _graphicsDevice.CreateTexture((int)width, (int)height, SampleCountFlags.Count1Bit, Format.D32_Float);

        _graphicsDevice.RecreateTexturesAction += (width, height) =>
        {
            _graphicsDevice.DestoryTexture(_depthTexture!);
            _depthTexture = _graphicsDevice.CreateTexture(width, height, SampleCountFlags.Count1Bit, Format.D32_Float);
        };
    }

    protected override void Draw(CommandBuffer commandBuffer, int imageIndex)
    {
        var backBuffer = _graphicsDevice.GetBackBuffer(imageIndex);
        var (width, height) = _graphicsDevice.GetSwapchainSizes();

        _graphicsDevice.BeginCommandBuffer(commandBuffer);
        {
            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal);
            _graphicsDevice.ImageBarrier(commandBuffer, _depthTexture!, ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal);

            _graphicsDevice.BeginRendering(commandBuffer, backBuffer, _depthTexture);
            {
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _depthBufferingGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4.CreateRotationZ(MathHelpers.DegreesToRadians(-180.0f)),
                    View = Matrix4X4.CreateLookAt<float>(new(-2.0f, 2.0f, 2.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 1.0f)),
                    Projection = Matrix4X4.CreatePerspectiveFieldOfView(MathHelpers.DegreesToRadians(45.0f), width / height, 0.1f, 10.0f)
                };

                var frameIndex = _currentFrame % Constants.MaxFramesInFlight;

                var cb = _depthBufferingConstantBuffers[frameIndex];
                _graphicsDevice.CopyDataToConstantBuffer(cb, ubo.Bytes);

                var descriptorWriter = _graphicsDevice.GetDescriptorWriter();
                var descriptorSet = _graphicsDevice.GetDescriptorSet(frameIndex, _depthBufferingGraphicsPipelineLayout);
                _graphicsDevice.BindConstantBuffer(descriptorWriter, 0, cb, cb.Size, 0, DescriptorType.UniformBuffer);
                _graphicsDevice.BindTexture(descriptorWriter, 1, _texture, _textureSampler, ImageLayout.ShaderReadOnlyOptimal, DescriptorType.CombinedImageSampler);
                _graphicsDevice.UpdateDescriptorSet(descriptorWriter, descriptorSet);
                _graphicsDevice.BindDescriptorSet(commandBuffer, _depthBufferingGraphicsPipelineLayout, descriptorSet);

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
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_depthBufferingVertexBuffer]);
                _graphicsDevice.BindIndexBuffer(commandBuffer, _depthBufferingIndexBuffer, IndexType.Uint16);
                _graphicsDevice.DrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
