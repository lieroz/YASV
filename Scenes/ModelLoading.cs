using System.Runtime.InteropServices;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using SkiaSharp;
using YASV.GraphicsEntities;
using YASV.Helpers;
using YASV.RHI;

namespace YASV.Scenes;

[Scene]
public class ModelLoading : BaseScene
{
    private readonly GraphicsPipelineLayout _modelLoadingGraphicsPipelineLayout;
    private readonly GraphicsPipelineDesc _modelLoadingGraphicsPipelineDesc;
    private readonly GraphicsPipeline _modelLoadingGraphicsPipeline;
    private readonly ConstantBuffer[] _modelLoadingConstantBuffers = new ConstantBuffer[Constants.MaxFramesInFlight];
    private const SampleCountFlags MSAASamples = SampleCountFlags.Count8Bit;
    private RHI.Texture _msaaTexture;
    private RHI.Texture _depthTexture;
    private readonly TextureSampler _textureSampler;
    private Model[] _models;

    public ModelLoading(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var vertexShader = _graphicsDevice.CreateShader("Shaders/textureMapping.vert.hlsl", ShaderStage.Vertex);
        var fragmentShader = _graphicsDevice.CreateShader("Shaders/textureMapping.frag.hlsl", ShaderStage.Pixel);

        _modelLoadingGraphicsPipelineDesc = new GraphicsPipelineDescBuilder()
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
                SampleShadingEnable = true,
                SampleCountFlags = MSAASamples,
                MinSampleShading = 0.2f,
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

        _modelLoadingGraphicsPipelineLayout = _graphicsDevice.CreateGraphicsPipelineLayout(new()
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
        _modelLoadingGraphicsPipeline = _graphicsDevice.CreateGraphicsPipeline(_modelLoadingGraphicsPipelineDesc, _modelLoadingGraphicsPipelineLayout);

        _models = [.. ModelExtensions.LoadModels("Assets/viking_room.obj", (path) =>
        {
            var data = System.IO.File.ReadAllBytes(path);
            var image = SKImage.FromEncodedData(data);

            return _graphicsDevice.CreateTextureFromImage(image);
        })];

        for (int i = 0; i < _models.Length; i++)
        {
            ref var model = ref _models[i];
            int vertexBufferSize = Marshal.SizeOf<Vertex>() * model.Vertices.Length;
            model.VertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

            int indexBufferSize = sizeof(uint) * model.Indices.Length;
            model.IndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

            var vertexData = new byte[Marshal.SizeOf<Vertex>() * model.Vertices.Length];
            for (int j = 0; j < model.Vertices.Length; j++)
            {
                System.Buffer.BlockCopy(model.Vertices[j].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * j, Marshal.SizeOf<Vertex>());
            }

            var indexData = new byte[sizeof(uint) * model.Indices.Length];
            System.Buffer.BlockCopy(model.Indices, 0, indexData, 0, indexData.Length);

            _graphicsDevice.CopyDataToVertexBuffer(model.VertexBuffer, vertexData);
            _graphicsDevice.CopyDataToIndexBuffer(model.IndexBuffer, indexData);
        }

        for (int i = 0; i < _modelLoadingConstantBuffers.Length; i++)
        {
            _modelLoadingConstantBuffers[i] = graphicsDevice.CreateConstantBuffer(Marshal.SizeOf<UniformBufferObject>());
        }

        _graphicsDevice.DestroyShaders([vertexShader, fragmentShader]);

        DisposeUnmanaged += () =>
        {
            _graphicsDevice.DestroyGraphicsPipelines([_modelLoadingGraphicsPipeline]);
            _graphicsDevice.DestroyGraphicsPipelineLayouts([_modelLoadingGraphicsPipelineLayout]);

            foreach (var model in _models)
            {
                _graphicsDevice.DestroyVertexBuffer(model.VertexBuffer!);
                _graphicsDevice.DestroyIndexBuffer(model.IndexBuffer!);

                foreach (var texture in model.Textures)
                {
                    _graphicsDevice.DestoryTexture(texture);
                }
            }

            foreach (var constantBuffer in _modelLoadingConstantBuffers)
            {
                _graphicsDevice.DestroyConstantBuffer(constantBuffer);
            }

            _graphicsDevice.DestoryTexture(_msaaTexture!);
            _graphicsDevice.DestoryTexture(_depthTexture!);
            _graphicsDevice.DestroyTextureSampler(_textureSampler!);
        };
        DisposeManaged += () =>
        {
            _graphicsDevice.RecreateTexturesAction = null;
        };

        uint maxLod = uint.MaxValue;
        foreach (var model in _models)
        {
            foreach (var texture in model.Textures)
            {
                maxLod = Math.Min(maxLod, texture.MipLevels);
            }
        }

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
                MipLodBias = 0.0f,
                MinLod = 0.0f,
                MaxLod = maxLod
            }
        );

        var (width, height) = _graphicsDevice.GetSwapchainSizes();
        _msaaTexture = _graphicsDevice.CreateTexture((int)width, (int)height, MSAASamples, Format.B8G8R8A8_Unorm_SRGB);
        _depthTexture = _graphicsDevice.CreateTexture((int)width, (int)height, MSAASamples, Format.D32_Float);

        _graphicsDevice.RecreateTexturesAction += (width, height) =>
        {
            _graphicsDevice.DestoryTexture(_msaaTexture!);
            _msaaTexture = _graphicsDevice.CreateTexture(width, height, MSAASamples, Format.B8G8R8A8_Unorm_SRGB);

            _graphicsDevice.DestoryTexture(_depthTexture!);
            _depthTexture = _graphicsDevice.CreateTexture(width, height, MSAASamples, Format.D32_Float);
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

            _graphicsDevice.BeginRendering(commandBuffer, backBuffer, _depthTexture, _msaaTexture);
            {
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _modelLoadingGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4.CreateRotationX(MathHelpers.DegreesToRadians(90.0f)) * Matrix4X4.CreateRotationY(MathHelpers.DegreesToRadians(-45.0f)),
                    View = Camera.GetViewMatrix(),
                    Projection = Matrix4X4.CreatePerspectiveFieldOfView(MathHelpers.DegreesToRadians(45.0f), width / height, 0.1f, 10.0f)
                };

                var frameIndex = _currentFrame % Constants.MaxFramesInFlight;

                var cb = _modelLoadingConstantBuffers[frameIndex];
                _graphicsDevice.CopyDataToConstantBuffer(cb, ubo.Bytes);

                var descriptorWriter = _graphicsDevice.GetDescriptorWriter();
                var descriptorSet = _graphicsDevice.GetDescriptorSet(frameIndex, _modelLoadingGraphicsPipelineLayout);
                _graphicsDevice.BindConstantBuffer(descriptorWriter, 0, cb, cb.Size, 0, DescriptorType.UniformBuffer);

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

                foreach (var model in _models)
                {
                    for (int i = 0; i < model.Textures.Count; i++)
                    {
                        _graphicsDevice.BindTexture(descriptorWriter, i + 1, model.Textures[i], _textureSampler, ImageLayout.ShaderReadOnlyOptimal, DescriptorType.CombinedImageSampler);
                    }
                    _graphicsDevice.UpdateDescriptorSet(descriptorWriter, descriptorSet);

                    _graphicsDevice.BindDescriptorSet(commandBuffer, _modelLoadingGraphicsPipelineLayout, descriptorSet);
                    _graphicsDevice.BindVertexBuffers(commandBuffer, [model.VertexBuffer!]);
                    _graphicsDevice.BindIndexBuffer(commandBuffer, model.IndexBuffer!, IndexType.Uint32);
                    _graphicsDevice.DrawIndexed(commandBuffer, (uint)model.Indices.Length, 1, 0, 0, 0);
                }
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}

