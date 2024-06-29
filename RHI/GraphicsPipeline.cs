namespace YASV.RHI;

public class GraphicsPipelineLayout
{
    public readonly Shader[] _shaders = new Shader[(int)Shader.Stage.Count];

    public struct VertexInputState
    {
    }

    public readonly VertexInputState _vertexInputState;

    public struct InputAssemblyState
    {
        public PrimitiveTopology PrimitiveTopology { get; set; }
    }

    public readonly InputAssemblyState _inputAssemblyState;

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

    public readonly RasterizationState _rasterizationState;

    public struct MultisampleState
    {
        public bool SampleShadingEnable { get; set; }
        public SampleCountFlags SampleCountFlags { get; set; }
        public float MinSampleShading { get; set; }
        public int SampleMask;
        public bool AlphaCoverageEnable { get; set; }
        public bool AlphaToOneEnable { get; set; }
    }

    public readonly MultisampleState _multisampleState;

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

    public readonly ColorBlendAttachmentState[] _colorBlendAttachmentStates;

    public struct ColorBlendState
    {
        public bool LogicOpEnable { get; set; }
        public LogicOp LogicOp { get; set; }
        public int AttachmentCount { get; set; }
        public float[] BlendConstants { get; set; }
    }

    public readonly ColorBlendState _colorBlendState;
}

public class GraphicsPipeline
{
}
