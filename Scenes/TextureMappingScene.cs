using System.Runtime.InteropServices;
using Silk.NET.Maths;
using SkiaSharp;
using YASV.Helpers;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class TextureMappingScene : BaseScene
{
    private readonly GraphicsPipelineLayout _textureMappingGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _textureMappingGraphicsPipelineDesc;
    private readonly GraphicsPipeline _textureMappingGraphicsPipeline;
    private readonly VertexBuffer _textureMappingVertexBuffer;
    private readonly IndexBuffer _textureMappingIndexBuffer;
    private readonly ConstantBuffer[] _textureMappingConstantBuffers = new ConstantBuffer[Constants.MaxFramesInFlight];
    private readonly Texture _texture;
    private readonly TextureSampler _textureSampler;

    private readonly Vertex[] _vertices =
    [
        new(new(-0.5f, -0.5f, 0.0f), new(1.0f, 0.0f, 0.0f), new(1.0f, 0.0f)),
        new(new(0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f), new(0.0f, 0.0f)),
        new(new(0.5f, 0.5f, 0.0f), new(0.0f, 0.0f, 1.0f), new(0.0f, 1.0f)),
        new(new(-0.5f, 0.5f, 0.0f), new(1.0f, 1.0f, 1.0f), new(1.0f, 1.0f))
    ];

    private readonly short[] _indices = [0, 1, 2, 2, 3, 0];

    public TextureMappingScene(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/textureMapping.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/textureMapping.frag.hlsl", ShaderStage.Pixel);

        _textureMappingGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
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

        _textureMappingGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
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
        _textureMappingGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_textureMappingGraphicsPipelineDesc, _textureMappingGraphicsPipelineLayout);

        int vertexBufferSize = Marshal.SizeOf<Vertex>() * _vertices.Length;
        _textureMappingVertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

        int indexBufferSize = sizeof(short) * _indices.Length;
        _textureMappingIndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

        var vertexData = new byte[Marshal.SizeOf<Vertex>() * _vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
        {
            System.Buffer.BlockCopy(_vertices[i].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * i, Marshal.SizeOf<Vertex>());
        }

        var indexData = new byte[sizeof(short) * _indices.Length];
        System.Buffer.BlockCopy(_indices, 0, indexData, 0, indexData.Length);

        _graphicsDevice.CopyDataToVertexBuffer(_textureMappingVertexBuffer, vertexData);
        _graphicsDevice.CopyDataToIndexBuffer(_textureMappingIndexBuffer, indexData);

        for (int i = 0; i < _textureMappingConstantBuffers.Length; i++)
        {
            _textureMappingConstantBuffers[i] = graphicsDevice.CreateConstantBuffer(Marshal.SizeOf<UniformBufferObject>());
        }

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_textureMappingGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_textureMappingGraphicsPipelineLayout]);
            _graphicsDevice.DestroyVertexBuffer(_textureMappingVertexBuffer);
            _graphicsDevice.DestroyIndexBuffer(_textureMappingIndexBuffer);

            foreach (var constantBuffer in _textureMappingConstantBuffers)
            {
                _graphicsDevice.DestroyConstantBuffer(constantBuffer);
            }

            _graphicsDevice.DestoryTexture(_texture!);
            _graphicsDevice.DestroyTextureSampler(_textureSampler!);
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
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _textureMappingGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4.CreateRotationZ(MathHelpers.DegreesToRadians(90.0f)),
                    View = Matrix4X4.CreateLookAt<float>(new(2.0f, 2.0f, 2.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 1.0f)),
                    Projection = Matrix4X4.CreatePerspectiveFieldOfView(MathHelpers.DegreesToRadians(45.0f), width / height, 0.1f, 10.0f)
                };

                var frameIndex = _currentFrame % Constants.MaxFramesInFlight;

                var cb = _textureMappingConstantBuffers[frameIndex];
                _graphicsDevice.CopyDataToConstantBuffer(cb, ubo.Bytes);

                var descriptorWriter = _graphicsDevice.GetDescriptorWriter();
                var descriptorSet = _graphicsDevice.GetDescriptorSet(frameIndex, _textureMappingGraphicsPipelineLayout);
                _graphicsDevice.BindConstantBuffer(descriptorWriter, 0, cb, cb.Size, 0, DescriptorType.UniformBuffer);
                _graphicsDevice.BindTexture(descriptorWriter, 1, _texture, _textureSampler, ImageLayout.ShaderReadOnlyOptimal, DescriptorType.CombinedImageSampler);
                _graphicsDevice.UpdateDescriptorSet(descriptorWriter, descriptorSet);
                _graphicsDevice.BindDescriptorSet(commandBuffer, _textureMappingGraphicsPipelineLayout, descriptorSet);

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
                _graphicsDevice.BindVertexBuffers(commandBuffer, [_textureMappingVertexBuffer]);
                _graphicsDevice.BindIndexBuffer(commandBuffer, _textureMappingIndexBuffer, IndexType.Uint16);
                _graphicsDevice.DrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}
