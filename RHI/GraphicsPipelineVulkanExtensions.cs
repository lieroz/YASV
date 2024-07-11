using System;
using System.Linq;

namespace YASV.RHI;

internal class VulkanGraphicsPipelineWrapper(Silk.NET.Vulkan.Pipeline pipeline) : GraphicsPipeline
{
    public Silk.NET.Vulkan.Pipeline Pipeline { get; set; } = pipeline;
}

internal class VulkanGraphicsPipelineLayoutWrapper(Silk.NET.Vulkan.PipelineLayout pipelineLayout) : GraphicsPipelineLayout
{
    public Silk.NET.Vulkan.PipelineLayout PipelineLayout { get; set; } = pipelineLayout;
}

internal static class GraphicsPipelineVulkanExtensions
{
    internal static Silk.NET.Vulkan.Pipeline ToVulkanGraphicsPipeline(this GraphicsPipeline graphicsPipeline)
    {
        return ((VulkanGraphicsPipelineWrapper)graphicsPipeline).Pipeline;
    }

    internal static Silk.NET.Vulkan.Format ToVulkanFormat(this Format format)
    {
        return format switch
        {
            Format.Undefined => Silk.NET.Vulkan.Format.Undefined,
            Format.R32G32_Sfloat => Silk.NET.Vulkan.Format.R32G32Sfloat,
            Format.R32G32B32_Sfloat => Silk.NET.Vulkan.Format.R32G32B32A32Sfloat,
            _ => throw new NotSupportedException($"Format '{format}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.VertexInputRate ToVulkanVertexInputRate(this VertexInputRate rate)
    {
        return rate switch
        {
            VertexInputRate.Vertex => Silk.NET.Vulkan.VertexInputRate.Vertex,
            VertexInputRate.Instance => Silk.NET.Vulkan.VertexInputRate.Instance,
            _ => throw new NotSupportedException($"Vertex input rate '{rate}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.VertexInputBindingDescription ToVulkanVertexInputBindingDescription(this VertexInputBindingDesc bindingDescription)
    {
        return new()
        {
            Binding = (uint)bindingDescription.Binding,
            Stride = (uint)bindingDescription.Stride,
            InputRate = bindingDescription.InputRate.ToVulkanVertexInputRate()
        };
    }

    internal static Silk.NET.Vulkan.VertexInputAttributeDescription ToVulkanVertexInputAttributeDescription(this VertexInputAttributeDesc attributeDescription)
    {
        return new()
        {
            Location = (uint)attributeDescription.Location,
            Binding = (uint)attributeDescription.Binding,
            Format = attributeDescription.Format.ToVulkanFormat(),
            Offset = (uint)attributeDescription.Offset
        };
    }

    internal static unsafe Silk.NET.Vulkan.PipelineVertexInputStateCreateInfo ToVulkanVertexInputState(this VertexInputState vertexInputState)
    {
        fixed (Silk.NET.Vulkan.VertexInputBindingDescription* bindingDescriptions = vertexInputState.BindingDescriptions.Select(x => x.ToVulkanVertexInputBindingDescription()).ToArray())
        {
            fixed (Silk.NET.Vulkan.VertexInputAttributeDescription* attributeDescriptions = vertexInputState.AttributeDescriptions.Select(x => x.ToVulkanVertexInputAttributeDescription()).ToArray())
            {
                return new()
                {
                    SType = Silk.NET.Vulkan.StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = (uint)vertexInputState.BindingDescriptions.Length,
                    PVertexBindingDescriptions = bindingDescriptions,
                    VertexAttributeDescriptionCount = (uint)vertexInputState.AttributeDescriptions.Length,
                    PVertexAttributeDescriptions = attributeDescriptions
                };
            }
        }
    }

    internal static Silk.NET.Vulkan.PrimitiveTopology ToVulkanPrimitiveTopology(this PrimitiveTopology primitiveTopology)
    {
        return primitiveTopology switch
        {
            PrimitiveTopology.PointList => Silk.NET.Vulkan.PrimitiveTopology.PointList,
            PrimitiveTopology.LineList => Silk.NET.Vulkan.PrimitiveTopology.LineList,
            PrimitiveTopology.LineStrip => Silk.NET.Vulkan.PrimitiveTopology.LineStrip,
            PrimitiveTopology.TriangleList => Silk.NET.Vulkan.PrimitiveTopology.TriangleList,
            PrimitiveTopology.TriangleStrip => Silk.NET.Vulkan.PrimitiveTopology.TriangleStrip,
            PrimitiveTopology.TriangleFan => Silk.NET.Vulkan.PrimitiveTopology.TriangleFan,
            PrimitiveTopology.LineListWithAdjacency => Silk.NET.Vulkan.PrimitiveTopology.LineListWithAdjacency,
            PrimitiveTopology.LineStripWithAdjacency => Silk.NET.Vulkan.PrimitiveTopology.LineStripWithAdjacency,
            PrimitiveTopology.TriangleListWithAdjacency => Silk.NET.Vulkan.PrimitiveTopology.TriangleListWithAdjacency,
            PrimitiveTopology.TriangleStripWithAdjacency => Silk.NET.Vulkan.PrimitiveTopology.TriangleStripWithAdjacency,
            PrimitiveTopology.PatchList => Silk.NET.Vulkan.PrimitiveTopology.PatchList,
            _ => throw new NotSupportedException($"Primitive topology '{primitiveTopology}' is not supported.")
        };
    }

    internal static Silk.NET.Vulkan.PipelineInputAssemblyStateCreateInfo ToVulkanInputAssemblyState(this InputAssemblyState inputAssemblyState)
    {
        return new()
        {
            SType = Silk.NET.Vulkan.StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = inputAssemblyState.PrimitiveTopology.ToVulkanPrimitiveTopology(),
            PrimitiveRestartEnable = false
        };
    }

    internal static Silk.NET.Vulkan.PolygonMode ToVulkanPolygonMode(this PolygonMode polygonMode)
    {
        return polygonMode switch
        {
            PolygonMode.Fill => Silk.NET.Vulkan.PolygonMode.Fill,
            PolygonMode.Line => Silk.NET.Vulkan.PolygonMode.Line,
            PolygonMode.Point => Silk.NET.Vulkan.PolygonMode.Point,
            _ => throw new NotSupportedException($"Polygon mode '{polygonMode}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.CullModeFlags ToVulkanCullModeFlags(this CullModeFlags cullModeFlags)
    {
        return cullModeFlags switch
        {
            CullModeFlags.None => Silk.NET.Vulkan.CullModeFlags.None,
            CullModeFlags.FrontBit => Silk.NET.Vulkan.CullModeFlags.FrontBit,
            CullModeFlags.BackBit => Silk.NET.Vulkan.CullModeFlags.BackBit,
            CullModeFlags.FrontAndBack => Silk.NET.Vulkan.CullModeFlags.FrontAndBack,
            _ => throw new NotSupportedException($"Cull mode '{cullModeFlags}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.FrontFace ToVulkanFrontFace(this FrontFace frontFace)
    {
        return frontFace switch
        {
            FrontFace.CounterClockwise => Silk.NET.Vulkan.FrontFace.CounterClockwise,
            FrontFace.Clockwise => Silk.NET.Vulkan.FrontFace.Clockwise,
            _ => throw new NotSupportedException($"Front face '{frontFace}' not supported.")
        };

    }

    internal static Silk.NET.Vulkan.PipelineRasterizationStateCreateInfo ToVulkanRasterizationState(this RasterizationState rasterizationState)
    {
        return new()
        {
            SType = Silk.NET.Vulkan.StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = rasterizationState.DepthClampEnable,
            RasterizerDiscardEnable = rasterizationState.RasterizerDiscardEnable,
            PolygonMode = rasterizationState.PolygonMode.ToVulkanPolygonMode(),
            LineWidth = rasterizationState.LineWidth,
            CullMode = rasterizationState.CullMode.ToVulkanCullModeFlags(),
            FrontFace = rasterizationState.FrontFace.ToVulkanFrontFace(),
            DepthBiasEnable = rasterizationState.DepthBiasEnable,
            DepthBiasConstantFactor = rasterizationState.DepthBiasConstantFactor,
            DepthBiasClamp = rasterizationState.DepthBiasClamp,
            DepthBiasSlopeFactor = rasterizationState.DepthBiasSlopeFactor
        };
    }

    internal static Silk.NET.Vulkan.SampleCountFlags ToVulkanSampleCountFlags(this SampleCountFlags sampleCountFlags)
    {
        return sampleCountFlags switch
        {
            SampleCountFlags.None => Silk.NET.Vulkan.SampleCountFlags.None,
            SampleCountFlags.Count1Bit => Silk.NET.Vulkan.SampleCountFlags.Count1Bit,
            SampleCountFlags.Count2Bit => Silk.NET.Vulkan.SampleCountFlags.Count2Bit,
            SampleCountFlags.Count4Bit => Silk.NET.Vulkan.SampleCountFlags.Count4Bit,
            SampleCountFlags.Count8Bit => Silk.NET.Vulkan.SampleCountFlags.Count8Bit,
            SampleCountFlags.Count16Bit => Silk.NET.Vulkan.SampleCountFlags.Count16Bit,
            SampleCountFlags.Count32Bit => Silk.NET.Vulkan.SampleCountFlags.Count32Bit,
            SampleCountFlags.Count64Bit => Silk.NET.Vulkan.SampleCountFlags.Count64Bit,
            _ => throw new NotSupportedException($"Rasterization samples '{sampleCountFlags}' not supported.")
        };
    }

    internal static unsafe Silk.NET.Vulkan.PipelineMultisampleStateCreateInfo ToVulkanMultisampleState(this MultisampleState multisampleState)
    {
        fixed (int* pSampleMask = multisampleState.SampleMask)
        {
            return new()
            {
                SType = Silk.NET.Vulkan.StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = multisampleState.SampleShadingEnable,
                RasterizationSamples = multisampleState.SampleCountFlags.ToVulkanSampleCountFlags(),
                MinSampleShading = multisampleState.MinSampleShading,
                PSampleMask = (uint*)pSampleMask,
                AlphaToCoverageEnable = multisampleState.AlphaCoverageEnable,
                AlphaToOneEnable = multisampleState.AlphaToOneEnable
            };
        }
    }

    internal static Silk.NET.Vulkan.CompareOp ToVulkanCompareOp(this CompareOp compareOp)
    {
        return compareOp switch
        {
            CompareOp.Never => Silk.NET.Vulkan.CompareOp.Never,
            CompareOp.Less => Silk.NET.Vulkan.CompareOp.Less,
            CompareOp.Equal => Silk.NET.Vulkan.CompareOp.Equal,
            CompareOp.LessOrEqual => Silk.NET.Vulkan.CompareOp.LessOrEqual,
            CompareOp.Greater => Silk.NET.Vulkan.CompareOp.Greater,
            CompareOp.NotEqual => Silk.NET.Vulkan.CompareOp.NotEqual,
            CompareOp.GreaterOrEqual => Silk.NET.Vulkan.CompareOp.GreaterOrEqual,
            CompareOp.Always => Silk.NET.Vulkan.CompareOp.Always,
            _ => throw new NotSupportedException($"Compare operation '{compareOp}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.StencilOp ToVulkanStencilOp(this StencilOp stencilOp)
    {
        return stencilOp switch
        {
            StencilOp.Keep => Silk.NET.Vulkan.StencilOp.Keep,
            StencilOp.Zero => Silk.NET.Vulkan.StencilOp.Zero,
            StencilOp.Replace => Silk.NET.Vulkan.StencilOp.Replace,
            StencilOp.IncrementAndClamp => Silk.NET.Vulkan.StencilOp.IncrementAndClamp,
            StencilOp.DecrementAndClamp => Silk.NET.Vulkan.StencilOp.DecrementAndClamp,
            StencilOp.Invert => Silk.NET.Vulkan.StencilOp.Invert,
            StencilOp.IncrementAndWrap => Silk.NET.Vulkan.StencilOp.IncrementAndWrap,
            StencilOp.DecrementAndWrap => Silk.NET.Vulkan.StencilOp.DecrementAndWrap,
            _ => throw new NotSupportedException($"Stencil operation '{stencilOp}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.StencilOpState ToVulkanStencilOpState(this StencilOpState stencilOpState)
    {
        return new()
        {
            FailOp = stencilOpState.FailOp.ToVulkanStencilOp(),
            PassOp = stencilOpState.PassOp.ToVulkanStencilOp(),
            DepthFailOp = stencilOpState.DepthFailOp.ToVulkanStencilOp(),
            CompareOp = stencilOpState.CompareOp.ToVulkanCompareOp(),
            CompareMask = (uint)stencilOpState.CompareMask,
            WriteMask = (uint)stencilOpState.WriteMask,
            Reference = (uint)stencilOpState.Reference
        };
    }

    internal static Silk.NET.Vulkan.PipelineDepthStencilStateCreateInfo ToVulkanDepthStencilState(this DepthStencilState depthStencilState)
    {
        return new()
        {
            SType = Silk.NET.Vulkan.StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = depthStencilState.DepthTestEnable,
            DepthWriteEnable = depthStencilState.DepthWriteEnable,
            DepthCompareOp = depthStencilState.DepthCompareOp.ToVulkanCompareOp(),
            DepthBoundsTestEnable = depthStencilState.DepthBoundsTestEnable,
            StencilTestEnable = depthStencilState.StencilTestEnable,
            Front = depthStencilState.Front.ToVulkanStencilOpState(),
            Back = depthStencilState.Back.ToVulkanStencilOpState(),
            MinDepthBounds = depthStencilState.MinDepthBounds,
            MaxDepthBounds = depthStencilState.MaxDepthBounds
        };
    }

    internal static Silk.NET.Vulkan.BlendFactor ToVulkanBlendFactor(this BlendFactor blendFactor)
    {
        return blendFactor switch
        {
            BlendFactor.Zero => Silk.NET.Vulkan.BlendFactor.Zero,
            BlendFactor.One => Silk.NET.Vulkan.BlendFactor.One,
            BlendFactor.SrcColor => Silk.NET.Vulkan.BlendFactor.SrcColor,
            BlendFactor.OneMinusSrcColor => Silk.NET.Vulkan.BlendFactor.OneMinusSrcColor,
            BlendFactor.DstColor => Silk.NET.Vulkan.BlendFactor.DstColor,
            BlendFactor.OneMinusDstColor => Silk.NET.Vulkan.BlendFactor.OneMinusDstColor,
            BlendFactor.SrcAlpha => Silk.NET.Vulkan.BlendFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => Silk.NET.Vulkan.BlendFactor.OneMinusSrcAlpha,
            BlendFactor.DstAlpha => Silk.NET.Vulkan.BlendFactor.DstAlpha,
            BlendFactor.OneMinusDstAlpha => Silk.NET.Vulkan.BlendFactor.OneMinusDstAlpha,
            BlendFactor.ConstantColor => Silk.NET.Vulkan.BlendFactor.ConstantColor,
            BlendFactor.OneMinusConstantColor => Silk.NET.Vulkan.BlendFactor.OneMinusConstantColor,
            BlendFactor.ConstantAlpha => Silk.NET.Vulkan.BlendFactor.ConstantAlpha,
            BlendFactor.OneMinusConstantAlpha => Silk.NET.Vulkan.BlendFactor.OneMinusConstantAlpha,
            BlendFactor.SrcAlphaSaturate => Silk.NET.Vulkan.BlendFactor.SrcAlphaSaturate,
            BlendFactor.Src1Color => Silk.NET.Vulkan.BlendFactor.Src1Color,
            BlendFactor.OneMinusSrc1Color => Silk.NET.Vulkan.BlendFactor.OneMinusSrc1Color,
            BlendFactor.Src1Alpha => Silk.NET.Vulkan.BlendFactor.Src1Alpha,
            BlendFactor.OneMinusSrc1Alpha => Silk.NET.Vulkan.BlendFactor.OneMinusSrc1Alpha,
            _ => throw new NotSupportedException($"Blend factor '{blendFactor}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.BlendOp ToVulkanBlendOp(this BlendOp blendOperation)
    {
        return blendOperation switch
        {
            BlendOp.Add => Silk.NET.Vulkan.BlendOp.Add,
            BlendOp.Subtract => Silk.NET.Vulkan.BlendOp.Subtract,
            BlendOp.ReverseSubtract => Silk.NET.Vulkan.BlendOp.ReverseSubtract,
            BlendOp.Min => Silk.NET.Vulkan.BlendOp.Min,
            BlendOp.Max => Silk.NET.Vulkan.BlendOp.Max,
            _ => throw new NotSupportedException($"Blend operation '{blendOperation}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.ColorComponentFlags ToVulkanColorComponentFlags(this ColorComponentFlags colorComponentFlags)
    {
        return colorComponentFlags switch
        {
            ColorComponentFlags.RBit => Silk.NET.Vulkan.ColorComponentFlags.RBit,
            ColorComponentFlags.GBit => Silk.NET.Vulkan.ColorComponentFlags.GBit,
            ColorComponentFlags.BBit => Silk.NET.Vulkan.ColorComponentFlags.BBit,
            ColorComponentFlags.ABit => Silk.NET.Vulkan.ColorComponentFlags.ABit,
            _ => throw new NotSupportedException($"Color component '{colorComponentFlags}' not supported.")
        };
    }

    internal static Silk.NET.Vulkan.PipelineColorBlendAttachmentState ToVulkanColorBlendAttachmentState(
        this ColorBlendAttachmentState colorBlendAttachmentState)
    {
        var colorWriteMask = Silk.NET.Vulkan.ColorComponentFlags.None;
        foreach (var component in colorBlendAttachmentState.ColorComponentFlags)
        {
            colorWriteMask |= component.ToVulkanColorComponentFlags();
        }

        return new()
        {
            ColorWriteMask = colorWriteMask,
            BlendEnable = colorBlendAttachmentState.BlendEnable,
            SrcColorBlendFactor = colorBlendAttachmentState.SrcAlphaBlendFactor.ToVulkanBlendFactor(),
            DstColorBlendFactor = colorBlendAttachmentState.DstColorBlendFactor.ToVulkanBlendFactor(),
            ColorBlendOp = colorBlendAttachmentState.ColorBlendOp.ToVulkanBlendOp(),
            SrcAlphaBlendFactor = colorBlendAttachmentState.SrcAlphaBlendFactor.ToVulkanBlendFactor(),
            DstAlphaBlendFactor = colorBlendAttachmentState.DstAlphaBlendFactor.ToVulkanBlendFactor(),
            AlphaBlendOp = colorBlendAttachmentState.AlphaBlendOp.ToVulkanBlendOp()
        };
    }

    internal static Silk.NET.Vulkan.LogicOp ToVulkanLogicOp(this LogicOp logicOp)
    {
        return logicOp switch
        {
            LogicOp.Clear => Silk.NET.Vulkan.LogicOp.Clear,
            LogicOp.And => Silk.NET.Vulkan.LogicOp.And,
            LogicOp.AndReverse => Silk.NET.Vulkan.LogicOp.AndReverse,
            LogicOp.Copy => Silk.NET.Vulkan.LogicOp.Copy,
            LogicOp.AndInverted => Silk.NET.Vulkan.LogicOp.AndInverted,
            LogicOp.NoOp => Silk.NET.Vulkan.LogicOp.NoOp,
            LogicOp.Xor => Silk.NET.Vulkan.LogicOp.Xor,
            LogicOp.Or => Silk.NET.Vulkan.LogicOp.Or,
            LogicOp.Nor => Silk.NET.Vulkan.LogicOp.Nor,
            LogicOp.Equivalent => Silk.NET.Vulkan.LogicOp.Equivalent,
            LogicOp.Invert => Silk.NET.Vulkan.LogicOp.Invert,
            LogicOp.OrReverse => Silk.NET.Vulkan.LogicOp.OrReverse,
            LogicOp.CopyInverted => Silk.NET.Vulkan.LogicOp.CopyInverted,
            LogicOp.OrInverted => Silk.NET.Vulkan.LogicOp.OrInverted,
            LogicOp.Nand => Silk.NET.Vulkan.LogicOp.Nand,
            LogicOp.Set => Silk.NET.Vulkan.LogicOp.Set,
            _ => throw new NotSupportedException($"Logic operation '{logicOp}' not supported.")
        };
    }

    internal static unsafe Silk.NET.Vulkan.PipelineColorBlendStateCreateInfo ToVulkanColorBlendState(this ColorBlendState colorBlendState,
                                                                                   Silk.NET.Vulkan.PipelineColorBlendAttachmentState[] attachmentStates)
    {
        fixed (Silk.NET.Vulkan.PipelineColorBlendAttachmentState* pAttachments = attachmentStates)
        {
            var vulkanColorBlendState = new Silk.NET.Vulkan.PipelineColorBlendStateCreateInfo()
            {
                SType = Silk.NET.Vulkan.StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = colorBlendState.LogicOpEnable,
                LogicOp = colorBlendState.LogicOp.ToVulkanLogicOp(),
                AttachmentCount = (uint)attachmentStates.Length,
                PAttachments = pAttachments
            };

            for (int i = 0; i < 4; i++)
            {
                vulkanColorBlendState.BlendConstants[i] = colorBlendState.BlendConstants[i];
            }

            return vulkanColorBlendState;
        }
    }

    internal static Silk.NET.Vulkan.PipelineLayout ToVulkanGraphicsPipelineLayout(this GraphicsPipelineLayout layout)
    {
        return ((VulkanGraphicsPipelineLayoutWrapper)layout).PipelineLayout;
    }
}
