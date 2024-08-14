using System.Runtime.InteropServices;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using SkiaSharp;
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
    private RHI.Texture _depthTexture;
    private readonly TextureSampler _textureSampler;
    private readonly Assimp _assimp = Assimp.GetApi();

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

        LoadModel("Assets/viking_room.obj");

        for (int i = 0; i < _meshes!.Length; i++)
        {
            ref var mesh = ref _meshes[i];
            int vertexBufferSize = Marshal.SizeOf<Vertex>() * mesh.Vertices.Length;
            mesh.VertexBuffer = _graphicsDevice.CreateVertexBuffer(vertexBufferSize);

            int indexBufferSize = sizeof(uint) * _meshes.First().Indices.Length;
            mesh.IndexBuffer = _graphicsDevice.CreateIndexBuffer(indexBufferSize);

            var vertexData = new byte[Marshal.SizeOf<Vertex>() * _meshes.First().Vertices.Length];
            for (int j = 0; j < mesh.Vertices.Length; j++)
            {
                System.Buffer.BlockCopy(mesh.Vertices[j].Bytes, 0, vertexData, Marshal.SizeOf<Vertex>() * j, Marshal.SizeOf<Vertex>());
            }

            var indexData = new byte[sizeof(uint) * mesh.Indices.Length];
            System.Buffer.BlockCopy(mesh.Indices, 0, indexData, 0, indexData.Length);

            _graphicsDevice.CopyDataToVertexBuffer(mesh.VertexBuffer, vertexData);
            _graphicsDevice.CopyDataToIndexBuffer(mesh.IndexBuffer, indexData);
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

            foreach (var mesh in _meshes)
            {
                _graphicsDevice.DestroyVertexBuffer(mesh.VertexBuffer);
                _graphicsDevice.DestroyIndexBuffer(mesh.IndexBuffer);

                foreach (var texture in mesh.Textures)
                {
                    _graphicsDevice.DestoryTexture(texture);
                }
            }

            foreach (var constantBuffer in _modelLoadingConstantBuffers)
            {
                _graphicsDevice.DestroyConstantBuffer(constantBuffer);
            }

            _graphicsDevice.DestoryTexture(_depthTexture!);
            _graphicsDevice.DestroyTextureSampler(_textureSampler!);
        };
        DisposeManaged += () =>
        {
            _graphicsDevice.RecreateTexturesAction = null;
        };


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
        _depthTexture = _graphicsDevice.CreateTexture((int)width, (int)height, Format.D32_Float);

        _graphicsDevice.RecreateTexturesAction += (width, height) =>
        {
            _graphicsDevice.DestoryTexture(_depthTexture!);
            _depthTexture = _graphicsDevice.CreateTexture(width, height, Format.D32_Float);
        };
    }

    private unsafe void LoadModel(string path)
    {
        var scene = _assimp.ImportFile(path, (uint)PostProcessSteps.Triangulate);

        if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        ProcessNode(scene->MRootNode, scene);
    }

    private struct Mesh
    {
        public Vertex[] Vertices { get; set; }
        public uint[] Indices { get; set; }
        public List<RHI.Texture> Textures { get; set; }
        public VertexBuffer VertexBuffer { get; set; }
        public IndexBuffer IndexBuffer { get; set; }
    }

    private Mesh[] _meshes;

    private unsafe void ProcessNode(Node* node, Scene* scene)
    {
        _meshes = new Mesh[node->MNumMeshes];
        for (var i = 0; i < node->MNumMeshes; i++)
        {
            var mesh = scene->MMeshes[node->MMeshes[i]];
            _meshes[i] = ProcessMesh(mesh, scene);

        }

        for (var i = 0; i < node->MNumChildren; i++)
        {
            ProcessNode(node->MChildren[i], scene);
        }
    }

    private unsafe Mesh ProcessMesh(Silk.NET.Assimp.Mesh* mesh, Scene* scene)
    {
        var vertices = new Vertex[mesh->MNumVertices];
        var indices = new List<uint>();
        var textures = new List<RHI.Texture>();

        for (uint i = 0; i < mesh->MNumVertices; i++)
        {
            var mv = mesh->MVertices[i];
            var tc = mesh->MTextureCoords[0][i];
            vertices[i] = new Vertex(new(mv.X, mv.Y, mv.Z), new(1.0f, 1.0f, 1.0f), new(tc.X, 1.0f - tc.Y));
        }

        for (uint i = 0; i < mesh->MNumFaces; i++)
        {
            Face face = mesh->MFaces[i];
            for (uint j = 0; j < face.MNumIndices; j++)
            {
                indices.Add(face.MIndices[j]);
            }
        }

        Material* material = scene->MMaterials[mesh->MMaterialIndex];

        var diffuseMaps = LoadMaterialTextures(material, TextureType.Diffuse);
        if (diffuseMaps.Count != 0)
        {
            textures.AddRange(diffuseMaps);
        }

        return new Mesh()
        {
            Vertices = vertices,
            Indices = [.. indices],
            Textures = textures
        };
    }

    private unsafe List<RHI.Texture> LoadMaterialTextures(Material* mat, TextureType type)
    {
        var textureCount = _assimp.GetMaterialTextureCount(mat, type);
        var textures = new List<RHI.Texture>();
        for (uint i = 0; i < textureCount; i++)
        {
            AssimpString path;
            _assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);

            var data = System.IO.File.ReadAllBytes(path);
            var image = SKImage.FromEncodedData(data);

            var texture = _graphicsDevice.CreateTextureFromImage(image);
            textures.Add(texture);
        }
        return textures;
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
                _graphicsDevice.BindGraphicsPipeline(commandBuffer, _modelLoadingGraphicsPipeline);

                var ubo = new UniformBufferObject()
                {
                    Model = Matrix4X4<float>.Identity,
                    View = Matrix4X4.CreateLookAt<float>(new(2.0f, 2.0f, 2.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, -1.0f)),
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

                foreach (var mesh in _meshes)
                {
                    for (int i = 0; i < mesh.Textures.Count; i++)
                    {
                        _graphicsDevice.BindTexture(descriptorWriter, i + 1, mesh.Textures[i], _textureSampler, ImageLayout.ShaderReadOnlyOptimal, DescriptorType.CombinedImageSampler);
                    }
                    _graphicsDevice.UpdateDescriptorSet(descriptorWriter, descriptorSet);

                    _graphicsDevice.BindDescriptorSet(commandBuffer, _modelLoadingGraphicsPipelineLayout, descriptorSet);
                    _graphicsDevice.BindVertexBuffers(commandBuffer, [mesh.VertexBuffer]);
                    _graphicsDevice.BindIndexBuffer(commandBuffer, mesh.IndexBuffer, IndexType.Uint32);
                    _graphicsDevice.DrawIndexed(commandBuffer, (uint)mesh.Indices.Length, 1, 0, 0, 0);
                }
            }

            _graphicsDevice.EndRendering(commandBuffer);

            _graphicsDevice.ImageBarrier(commandBuffer, backBuffer, ImageLayout.ColorAttachmentOptimal, ImageLayout.Present);
        }
        _graphicsDevice.EndCommandBuffer(commandBuffer);
    }
}

