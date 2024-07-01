using System;
using System.Collections.Generic;

namespace YASV.RHI;

public struct VertexInputState
{
}

public struct InputAssemblyState
{
    public PrimitiveTopology PrimitiveTopology { get; set; }
}

public struct RasterizationState
{
    public bool DepthClampEnable { get; set; }
    public bool RasterizerDiscardEnable { get; set; }
    public PolygonMode PolygonMode { get; set; }
    public float LineWidth { get; set; }
    public CullModeFlags CullMode { get; set; }
    public FrontFace FrontFace { get; set; }
    public bool DepthBiasEnable { get; set; }
    public float DepthBiasConstantFactor { get; set; }
    public float DepthBiasClamp { get; set; }
    public float DepthBiasSlopeFactor { get; set; }
}

public struct MultisampleState
{
    public bool SampleShadingEnable { get; set; }
    public SampleCountFlags SampleCountFlags { get; set; }
    public float MinSampleShading { get; set; }
    public int[]? SampleMask { get; set; }
    public bool AlphaCoverageEnable { get; set; }
    public bool AlphaToOneEnable { get; set; }
}

public struct StencilOpState
{
    public StencilOp FailOp { get; set; }
    public StencilOp PassOp { get; set; }
    public StencilOp DepthFailOp { get; set; }
    public CompareOp CompareOp { get; set; }
    public int CompareMask { get; set; }
    public int WriteMask { get; set; }
    public int Reference { get; set; }
}

public struct DepthStencilState
{
    public bool DepthTestEnable { get; set; }
    public bool DepthWriteEnable { get; set; }
    public CompareOp DepthCompareOp { get; set; }
    public bool DepthBoundsTestEnable { get; set; }
    public bool StencilTestEnable { get; set; }
    public StencilOpState Front { get; set; }
    public StencilOpState Back { get; set; }
    public float MinDepthBounds { get; set; }
    public float MaxDepthBounds { get; set; }
}

public struct ColorBlendAttachmentState
{
    public ColorComponentFlags[] ColorComponentFlags { get; set; }
    public bool BlendEnable { get; set; }
    public BlendFactor SrcColorBlendFactor { get; set; }
    public BlendFactor DstColorBlendFactor { get; set; }
    public BlendOp ColorBlendOp { get; set; }
    public BlendFactor SrcAlphaBlendFactor { get; set; }
    public BlendFactor DstAlphaBlendFactor { get; set; }
    public BlendOp AlphaBlendOp { get; set; }
}

public struct ColorBlendState
{
    public bool LogicOpEnable { get; set; }
    public LogicOp LogicOp { get; set; }
    public int AttachmentCount { get; set; }
    public float[] BlendConstants { get; set; }
}

public class DescriptorSetLayoutDesc
{
}

public class PushConstantRange
{
}

// TODO: https://vkguide.dev/docs/extra-chapter/abstracting_descriptors/
public class GraphicsPipelineLayoutDesc
{
    public int SetLayoutCount { get; set; }
    public DescriptorSetLayoutDesc[]? SetLayouts { get; set; }
    public int PushConstantRangeCount { get; set; }
    public PushConstantRange[]? PushConstantRanges { get; set; }
}

public class GraphicsPipelineLayout { }

public class GraphicsPipelineDesc(Shader[] shaders,
                              VertexInputState vertexInputState,
                              InputAssemblyState inputAssemblyState,
                              RasterizationState rasterizationState,
                              MultisampleState multisampleState,
                              DepthStencilState? depthStencilState,
                              IList<ColorBlendAttachmentState> colorBlendAttachmentStates,
                              ColorBlendState colorBlendState)
{
    public Shader[] Shaders { get; private set; } = shaders;
    public VertexInputState VertexInputState { get; private set; } = vertexInputState;
    public InputAssemblyState InputAssemblyState { get; private set; } = inputAssemblyState;
    public RasterizationState RasterizationState { get; private set; } = rasterizationState;
    public MultisampleState MultisampleState { get; private set; } = multisampleState;
    public DepthStencilState? DepthStencilState { get; private set; } = depthStencilState;
    public IList<ColorBlendAttachmentState> ColorBlendAttachmentStates { get; private set; } = colorBlendAttachmentStates;
    public ColorBlendState ColorBlendState { get; private set; } = colorBlendState;
}

// TODO: Add validation for fields
public class GraphicsPipelineDescBuilder
{
    private readonly Shader[] _shaders = new Shader[(int)Shader.Stage.Count];
    private VertexInputState _vertexInputState;
    private InputAssemblyState _inputAssemblyState;
    private RasterizationState _rasterizationState;
    private MultisampleState _multisampleState;
    private DepthStencilState? _depthStencilState = null;
    private readonly IList<ColorBlendAttachmentState> _colorBlendAttachmentStates = [];
    private ColorBlendState _colorBlendState;

    public GraphicsPipelineDescBuilder SetVertexShader(Shader shader)
    {
        if (shader._stage != Shader.Stage.Vertex)
        {
            throw new ArgumentException($"Shader stage is invalid: {shader._stage}.");
        }
        _shaders[(int)Shader.Stage.Vertex] = shader;
        return this;
    }

    public GraphicsPipelineDescBuilder SetFragmentShader(Shader shader)
    {
        if (shader._stage != Shader.Stage.Fragment)
        {
            throw new ArgumentException($"Shader stage is invalid: {shader._stage}.");
        }
        _shaders[(int)Shader.Stage.Fragment] = shader;
        return this;
    }

    public GraphicsPipelineDescBuilder SetVertexInputState(VertexInputState vertexInputState)
    {
        _vertexInputState = vertexInputState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetInputAssemblyState(InputAssemblyState inputAssemblyState)
    {
        _inputAssemblyState = inputAssemblyState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetRasterizationState(RasterizationState rasterizationState)
    {
        _rasterizationState = rasterizationState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetMultisampleState(MultisampleState multisampleState)
    {
        _multisampleState = multisampleState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetDepthStencilState(DepthStencilState depthStencilState)
    {
        _depthStencilState = depthStencilState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetColorBlendAttachmentState(int index, ColorBlendAttachmentState colorBlendAttachmentState)
    {
        if (_colorBlendAttachmentStates.Count <= index)
        {
            for (int i = _colorBlendAttachmentStates.Count; i <= index; i++)
            {
                _colorBlendAttachmentStates.Add(default);
            }
        }
        _colorBlendAttachmentStates[index] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineDescBuilder SetColorBlendState(ColorBlendState colorBlendState)
    {
        _colorBlendState = colorBlendState;
        return this;
    }

    public GraphicsPipelineDesc Build()
    {
        return new(
            _shaders,
            _vertexInputState,
            _inputAssemblyState,
            _rasterizationState,
            _multisampleState,
            _depthStencilState,
            _colorBlendAttachmentStates,
            _colorBlendState);
    }
}

public class GraphicsPipeline { }