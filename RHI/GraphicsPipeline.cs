using System;

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
    public int SampleMask;
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
    public BlendOp ColorBlendOperation { get; set; }
    public BlendFactor SrcAlphaBlendFactor { get; set; }
    public BlendFactor DstAlphaBlendFactor { get; set; }
    public BlendOp AlphaBlendOperation { get; set; }
}

public struct ColorBlendState
{
    public bool LogicOpEnable { get; set; }
    public LogicOp LogicOp { get; set; }
    public int AttachmentCount { get; set; }
    public float[] BlendConstants { get; set; }
}

public class GraphicsPipelineLayout(Shader[] shaders,
                              VertexInputState vertexInputState,
                              InputAssemblyState inputAssemblyState,
                              RasterizationState rasterizationState,
                              MultisampleState multisampleState,
                              DepthStencilState depthStencilState,
                              ColorBlendAttachmentState[] colorBlendAttachmentStates,
                              ColorBlendState colorBlendState)
{
    public Shader[] Shaders { get; private set; } = shaders;
    public VertexInputState VertexInputState { get; private set; } = vertexInputState;
    public InputAssemblyState InputAssemblyState { get; private set; } = inputAssemblyState;
    public RasterizationState RasterizationState { get; private set; } = rasterizationState;
    public MultisampleState MultisampleState { get; private set; } = multisampleState;
    public DepthStencilState DepthStencilState { get; private set; } = depthStencilState;
    public ColorBlendAttachmentState[] ColorBlendAttachmentStates { get; private set; } = colorBlendAttachmentStates;
    public ColorBlendState ColorBlendState { get; private set; } = colorBlendState;
}

// TODO: Add validation for fields
public class GraphicsPipelineLayoutBuilder
{
    private readonly Shader[] _shaders = new Shader[(int)Shader.Stage.Count];
    private VertexInputState _vertexInputState;
    private InputAssemblyState _inputAssemblyState;
    private RasterizationState _rasterizationState;
    private MultisampleState _multisampleState;
    private DepthStencilState _depthStencilState;
    private readonly ColorBlendAttachmentState[] _colorBlendAttachmentStates = new ColorBlendAttachmentState[Constants.SimultaneousRenderTargetCount];
    private ColorBlendState _colorBlendState;

    public GraphicsPipelineLayoutBuilder SetVertexShader(Shader shader)
    {
        if (shader._stage != Shader.Stage.Vertex)
        {
            throw new ArgumentException($"Shader stage is invalid: {shader._stage}");
        }
        _shaders[(int)Shader.Stage.Vertex] = shader;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetFragmentShader(Shader shader)
    {
        if (shader._stage != Shader.Stage.Fragment)
        {
            throw new ArgumentException($"Shader stage is invalid: {shader._stage}");
        }
        _shaders[(int)Shader.Stage.Fragment] = shader;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetVertexInputState(VertexInputState vertexInputState)
    {
        _vertexInputState = vertexInputState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetInputAssemblyState(InputAssemblyState inputAssemblyState)
    {
        _inputAssemblyState = inputAssemblyState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRasterizationState(RasterizationState rasterizationState)
    {
        _rasterizationState = rasterizationState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetMultisampleState(MultisampleState multisampleState)
    {
        _multisampleState = multisampleState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetDepthStencilState(DepthStencilState depthStencilState)
    {
        _depthStencilState = depthStencilState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget0(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x0] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget1(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x1] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget2(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x2] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget3(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x3] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget4(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x4] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget5(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x5] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget6(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x6] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetRenderTarget7(ColorBlendAttachmentState colorBlendAttachmentState)
    {
        _colorBlendAttachmentStates[0x7] = colorBlendAttachmentState;
        return this;
    }

    public GraphicsPipelineLayoutBuilder SetColorBlendState(ColorBlendState colorBlendState)
    {
        _colorBlendState = colorBlendState;
        return this;
    }

    public GraphicsPipelineLayout Build()
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