namespace YASV.RHI;

public class GraphicsPipelineLayout
{
    #region ShaderStages
    public readonly Shader[] _shaders = new Shader[(int)Shader.Stage.Count];
    #endregion

    #region VertexInput
    public struct VertexInputState
    {
    }

    public readonly VertexInputState _vertexInputState;
    #endregion

    #region InputAssembly
    public enum Topology
    {
        PointList,
        LineList,
        LineStrip,
        TriangleList,
        TriangleStrip,
        TriangleFan,
        LineListWithAdjacency,
        LineStripWithAdjacency,
        TriangleListWithAdjacency,
        TriangleStripWithAdjacency,
        PatchList,
        Count
    }

    public struct InputAssemblyState
    {
        public Topology _topology;
    }

    public readonly InputAssemblyState _inputAssemblyState;
    #endregion

    #region Rasterization
    public enum PolygonMode
    {
        Fill,
        Line,
        Point,
        Count
    }

    public enum CullMode
    {
        None,
        Front,
        Back,
        FrontAndBack,
        Count
    }

    public enum FrontFace
    {
        CounterClock,
        Clock,
        Count
    }

    public struct RasterizationState
    {
        public bool _depthClampEnable;
        public bool _rasterizerDiscardEnable;
        public PolygonMode _polygonMode;
        public float _lineWidth;
        public CullMode _cullMode;
        public FrontFace _frontFace;
        public bool _depthBiasEnable;
        public float _depthBiasConstantFactor;
        public float _depthBiasClamp;
        public float _depthBiasSlopeFactor;
    }

    public readonly RasterizationState _rasterizationState;
    #endregion

    #region Multisampling
    public enum RasterizationSamples
    {
        Zero,
        One,
        Two,
        Four,
        Eight,
        Sixteen,
        ThrityTwo,
        SixtyFour,
        Count
    }

    public struct MultisampleState
    {
        public bool _sampleShadingEnable;
        public RasterizationSamples _rasterizationSamples;
        public float _minSampleShading;
        public int _sampleMask;
        public bool _alphaCoverageEnable;
        public bool _alphaToOneEnable;
    }

    public readonly MultisampleState _multisampleState;
    #endregion

    #region ColorBlendAttachmentState
    public enum ColorComponent
    {
        R,
        G,
        B,
        A,
        Count
    }

    public enum BlendFactor
    {
        Zero,
        One,
        SrcColor,
        OneMinusSrcColor,
        DstColor,
        OneMinusDstColor,
        SrcAlpha,
        OneMinusSrcAlpha,
        DstAlpha,
        OneMinusDstAlpha,
        ConstantColor,
        OneMinusConstantColor,
        ConstantAlpha,
        OneMinusConstantAlpha,
        SrcAlphaSaturate,
        Src1Color,
        OneMinusSrc1Color,
        Src1Alpha,
        OneMinusSrc1Alpha,
        Count
    }

    public enum BlendOperation
    {
        Add,
        Sub,
        ReverseSub,
        Min,
        Max,
        Count
    }

    public struct ColorBlendAttachmentState
    {
        public ColorComponent[] _colorComponents;
        public bool _blendEnable;
        public BlendFactor _srcColorBlendFactor;
        public BlendFactor _dstColorBlendFactor;
        public BlendOperation _colorBlendOperation;
        public BlendFactor _srcAlphaBlendFactor;
        public BlendFactor _dstAlphaBlendFactor;
        public BlendOperation _alphaBlendOperation;
    }

    public readonly ColorBlendAttachmentState[] _colorBlendAttachmentStates;
    #endregion

    #region ColorBlendState
    public enum LogicOperation
    {
        Clear,
        And,
        AndReverse,
        Copy,
        AndInverted,
        NoOp,
        Xor,
        Or,
        Nor,
        Equivalent,
        Invert,
        OrReverse,
        CopyInverted,
        OrInverted,
        Nand,
        Set,
        Count
    }

    public struct ColorBlendState
    {
        public bool _logicOperationEnable;
        public LogicOperation _logicOperation;
        public int _attachmentCount;
        public float[] _blendConstants;
    }

    public readonly ColorBlendState _colorBlendState;
    #endregion
}

public class GraphicsPipeline
{
}
