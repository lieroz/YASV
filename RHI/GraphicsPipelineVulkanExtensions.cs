using System;
using Silk.NET.Vulkan;

namespace YASV.RHI;

internal class VulkanGraphicsPipelineWrapper(Pipeline pipeline) : GraphicsPipeline
{
    public Pipeline _pipeline = pipeline;
}

internal static class GraphicsPipelineVulkanExtensions
{
    internal static Pipeline ToVulkanGraphicsPipeline(this GraphicsPipeline graphicsPipeline)
    {
        return ((VulkanGraphicsPipelineWrapper)graphicsPipeline)._pipeline;
    }

    internal static PipelineVertexInputStateCreateInfo ToVulkanVertexInputState(this GraphicsPipelineLayout.VertexInputState vertexInputState)
    {
        return new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
        };
    }

    internal static PipelineInputAssemblyStateCreateInfo ToVulkanInputAssemblyState(this GraphicsPipelineLayout.InputAssemblyState inputAssemblyState)
    {
        return new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = inputAssemblyState._topology switch
            {
                GraphicsPipelineLayout.Topology.PointList => PrimitiveTopology.PointList,
                GraphicsPipelineLayout.Topology.LineList => PrimitiveTopology.LineList,
                GraphicsPipelineLayout.Topology.LineStrip => PrimitiveTopology.LineStrip,
                GraphicsPipelineLayout.Topology.TriangleList => PrimitiveTopology.TriangleList,
                GraphicsPipelineLayout.Topology.TriangleStrip => PrimitiveTopology.TriangleStrip,
                GraphicsPipelineLayout.Topology.TriangleFan => PrimitiveTopology.TriangleFan,
                GraphicsPipelineLayout.Topology.LineListWithAdjacency => PrimitiveTopology.LineListWithAdjacency,
                GraphicsPipelineLayout.Topology.LineStripWithAdjacency => PrimitiveTopology.LineStripWithAdjacency,
                GraphicsPipelineLayout.Topology.TriangleListWithAdjacency => PrimitiveTopology.TriangleListWithAdjacency,
                GraphicsPipelineLayout.Topology.TriangleStripWithAdjacency => PrimitiveTopology.TriangleStripWithAdjacency,
                GraphicsPipelineLayout.Topology.PatchList => PrimitiveTopology.PatchList,
                _ => throw new NotImplementedException($"Primitive topology '{inputAssemblyState._topology}' is not supported.")
            },
            PrimitiveRestartEnable = false
        };
    }

    internal static PipelineRasterizationStateCreateInfo ToVulkanRasterizationState(this GraphicsPipelineLayout.RasterizationState rasterizationState)
    {
        return new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = rasterizationState._depthClampEnable,
            RasterizerDiscardEnable = rasterizationState._rasterizerDiscardEnable,
            PolygonMode = rasterizationState._polygonMode switch
            {
                GraphicsPipelineLayout.PolygonMode.Fill => PolygonMode.Fill,
                GraphicsPipelineLayout.PolygonMode.Line => PolygonMode.Line,
                GraphicsPipelineLayout.PolygonMode.Point => PolygonMode.Point,
                _ => throw new NotSupportedException($"Polygon mode '{rasterizationState._polygonMode}' not supported.")
            },
            LineWidth = rasterizationState._lineWidth,
            CullMode = rasterizationState._cullMode switch
            {
                GraphicsPipelineLayout.CullMode.None => CullModeFlags.None,
                GraphicsPipelineLayout.CullMode.Front => CullModeFlags.FrontBit,
                GraphicsPipelineLayout.CullMode.Back => CullModeFlags.BackBit,
                GraphicsPipelineLayout.CullMode.FrontAndBack => CullModeFlags.FrontAndBack,
                _ => throw new NotSupportedException($"Cull mode '{rasterizationState._cullMode}' not supported.")
            },
            FrontFace = rasterizationState._frontFace switch
            {
                GraphicsPipelineLayout.FrontFace.CounterClock => FrontFace.CounterClockwise,
                GraphicsPipelineLayout.FrontFace.Clock => FrontFace.Clockwise,
                _ => throw new NotSupportedException($"Front face '{rasterizationState._frontFace}' not supported.")
            },
            DepthBiasEnable = rasterizationState._depthBiasEnable,
            DepthBiasConstantFactor = rasterizationState._depthBiasConstantFactor,
            DepthBiasClamp = rasterizationState._depthBiasClamp,
            DepthBiasSlopeFactor = rasterizationState._depthBiasSlopeFactor
        };
    }

    internal static unsafe PipelineMultisampleStateCreateInfo ToVulkanMultisampleState(this GraphicsPipelineLayout.MultisampleState multisampleState)
    {
        return new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = multisampleState._sampleShadingEnable,
            RasterizationSamples = multisampleState._rasterizationSamples switch
            {
                GraphicsPipelineLayout.RasterizationSamples.Zero => SampleCountFlags.None,
                GraphicsPipelineLayout.RasterizationSamples.One => SampleCountFlags.Count1Bit,
                GraphicsPipelineLayout.RasterizationSamples.Two => SampleCountFlags.Count2Bit,
                GraphicsPipelineLayout.RasterizationSamples.Four => SampleCountFlags.Count4Bit,
                GraphicsPipelineLayout.RasterizationSamples.Eight => SampleCountFlags.Count8Bit,
                GraphicsPipelineLayout.RasterizationSamples.Sixteen => SampleCountFlags.Count16Bit,
                GraphicsPipelineLayout.RasterizationSamples.ThrityTwo => SampleCountFlags.Count32Bit,
                GraphicsPipelineLayout.RasterizationSamples.SixtyFour => SampleCountFlags.Count64Bit,
                _ => throw new NotSupportedException($"Rasterization samples '{multisampleState._rasterizationSamples}' not supported.")
            },
            MinSampleShading = multisampleState._minSampleShading,
            PSampleMask = (uint*)&multisampleState._sampleMask,
            AlphaToCoverageEnable = multisampleState._alphaCoverageEnable,
            AlphaToOneEnable = multisampleState._alphaToOneEnable
        };
    }

    internal static BlendFactor ToVulkanBlendFactor(this GraphicsPipelineLayout.BlendFactor blendFactor)
    {
        return blendFactor switch
        {
            GraphicsPipelineLayout.BlendFactor.Zero => BlendFactor.Zero,
            GraphicsPipelineLayout.BlendFactor.One => BlendFactor.One,
            GraphicsPipelineLayout.BlendFactor.SrcColor => BlendFactor.SrcColor,
            GraphicsPipelineLayout.BlendFactor.OneMinusSrcColor => BlendFactor.OneMinusSrcColor,
            GraphicsPipelineLayout.BlendFactor.DstColor => BlendFactor.DstColor,
            GraphicsPipelineLayout.BlendFactor.OneMinusDstColor => BlendFactor.OneMinusDstColor,
            GraphicsPipelineLayout.BlendFactor.SrcAlpha => BlendFactor.SrcAlpha,
            GraphicsPipelineLayout.BlendFactor.OneMinusSrcAlpha => BlendFactor.OneMinusSrcAlpha,
            GraphicsPipelineLayout.BlendFactor.DstAlpha => BlendFactor.DstAlpha,
            GraphicsPipelineLayout.BlendFactor.OneMinusDstAlpha => BlendFactor.OneMinusDstAlpha,
            GraphicsPipelineLayout.BlendFactor.ConstantColor => BlendFactor.ConstantColor,
            GraphicsPipelineLayout.BlendFactor.OneMinusConstantColor => BlendFactor.OneMinusConstantColor,
            GraphicsPipelineLayout.BlendFactor.ConstantAlpha => BlendFactor.ConstantAlpha,
            GraphicsPipelineLayout.BlendFactor.OneMinusConstantAlpha => BlendFactor.OneMinusConstantAlpha,
            GraphicsPipelineLayout.BlendFactor.SrcAlphaSaturate => BlendFactor.SrcAlphaSaturate,
            GraphicsPipelineLayout.BlendFactor.Src1Color => BlendFactor.Src1Color,
            GraphicsPipelineLayout.BlendFactor.OneMinusSrc1Color => BlendFactor.OneMinusSrc1Color,
            GraphicsPipelineLayout.BlendFactor.Src1Alpha => BlendFactor.Src1Alpha,
            GraphicsPipelineLayout.BlendFactor.OneMinusSrc1Alpha => BlendFactor.OneMinusSrc1Alpha,
            _ => throw new NotSupportedException($"Blend factor '{blendFactor}' not supported.")
        };
    }

    internal static BlendOp ToVulkanBlendOperation(this GraphicsPipelineLayout.BlendOperation blendOperation)
    {
        return blendOperation switch
        {
            GraphicsPipelineLayout.BlendOperation.Add => BlendOp.Add,
            GraphicsPipelineLayout.BlendOperation.Sub => BlendOp.Subtract,
            GraphicsPipelineLayout.BlendOperation.ReverseSub => BlendOp.ReverseSubtract,
            GraphicsPipelineLayout.BlendOperation.Min => BlendOp.Min,
            GraphicsPipelineLayout.BlendOperation.Max => BlendOp.Max,
            _ => throw new NotSupportedException($"Blend operation '{blendOperation}' not supported.")
        };
    }

    internal static PipelineColorBlendAttachmentState ToVulkanColorBlendAttachmentState(this GraphicsPipelineLayout.ColorBlendAttachmentState colorBlendAttachmentState)
    {
        ColorComponentFlags colorWriteMask = ColorComponentFlags.None;
        foreach (var component in colorBlendAttachmentState._colorComponents)
        {
            colorWriteMask |= component switch
            {
                GraphicsPipelineLayout.ColorComponent.R => ColorComponentFlags.RBit,
                GraphicsPipelineLayout.ColorComponent.G => ColorComponentFlags.GBit,
                GraphicsPipelineLayout.ColorComponent.B => ColorComponentFlags.BBit,
                GraphicsPipelineLayout.ColorComponent.A => ColorComponentFlags.ABit,
                _ => throw new NotSupportedException($"Color component '{component}' not supported.")
            };
        }

        return new()
        {
            ColorWriteMask = colorWriteMask,
            BlendEnable = colorBlendAttachmentState._blendEnable,
            SrcColorBlendFactor = colorBlendAttachmentState._srcAlphaBlendFactor.ToVulkanBlendFactor(),
            DstColorBlendFactor = colorBlendAttachmentState._dstColorBlendFactor.ToVulkanBlendFactor(),
            ColorBlendOp = colorBlendAttachmentState._colorBlendOperation.ToVulkanBlendOperation(),
            SrcAlphaBlendFactor = colorBlendAttachmentState._srcAlphaBlendFactor.ToVulkanBlendFactor(),
            DstAlphaBlendFactor = colorBlendAttachmentState._dstAlphaBlendFactor.ToVulkanBlendFactor(),
            AlphaBlendOp = colorBlendAttachmentState._alphaBlendOperation.ToVulkanBlendOperation()
        };
    }

    internal static unsafe PipelineColorBlendStateCreateInfo ToVulkanColorBlendState(this GraphicsPipelineLayout.ColorBlendState colorBlendState,
                                                                                   PipelineColorBlendAttachmentState[] attachmentStates)
    {
        fixed (PipelineColorBlendAttachmentState* attachmentStatesPtr = attachmentStates)
        {
            var vulkanColorBlendState = new PipelineColorBlendStateCreateInfo()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = colorBlendState._logicOperationEnable,
                LogicOp = colorBlendState._logicOperation switch
                {
                    GraphicsPipelineLayout.LogicOperation.Clear => LogicOp.Clear,
                    GraphicsPipelineLayout.LogicOperation.And => LogicOp.And,
                    GraphicsPipelineLayout.LogicOperation.AndReverse => LogicOp.AndReverse,
                    GraphicsPipelineLayout.LogicOperation.Copy => LogicOp.Copy,
                    GraphicsPipelineLayout.LogicOperation.AndInverted => LogicOp.AndInverted,
                    GraphicsPipelineLayout.LogicOperation.NoOp => LogicOp.NoOp,
                    GraphicsPipelineLayout.LogicOperation.Xor => LogicOp.Xor,
                    GraphicsPipelineLayout.LogicOperation.Or => LogicOp.Or,
                    GraphicsPipelineLayout.LogicOperation.Nor => LogicOp.Nor,
                    GraphicsPipelineLayout.LogicOperation.Equivalent => LogicOp.Equivalent,
                    GraphicsPipelineLayout.LogicOperation.Invert => LogicOp.Invert,
                    GraphicsPipelineLayout.LogicOperation.OrReverse => LogicOp.OrReverse,
                    GraphicsPipelineLayout.LogicOperation.CopyInverted => LogicOp.CopyInverted,
                    GraphicsPipelineLayout.LogicOperation.OrInverted => LogicOp.OrInverted,
                    GraphicsPipelineLayout.LogicOperation.Nand => LogicOp.Nand,
                    GraphicsPipelineLayout.LogicOperation.Set => LogicOp.Set,
                    _ => throw new NotSupportedException($"Logic operation '{colorBlendState._logicOperation}' not supported.")
                },
                AttachmentCount = (uint)attachmentStates.Length,
                PAttachments = attachmentStatesPtr
            };

            for (int i = 0; i < 4; i++)
            {
                vulkanColorBlendState.BlendConstants[i] = colorBlendState._blendConstants[i];
            }

            return vulkanColorBlendState;
        }
    }
}
