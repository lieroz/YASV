namespace YASV.RHI;

public enum VertexInputRate
{
    Vertex,
    Instance
}

// TODO: add more formats
public enum Format
{
    Undefined,
    R32G32_Sfloat,
    R32G32B32_Sfloat
}

public enum PrimitiveTopology
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
    PatchList
}

public enum PolygonMode
{
    Fill,
    Line,
    Point,
}

public enum CullModeFlags
{
    None,
    FrontBit,
    BackBit,
    FrontAndBack
}

public enum FrontFace
{
    CounterClockwise,
    Clockwise
}

public enum SampleCountFlags
{
    None,
    Count1Bit,
    Count2Bit,
    Count4Bit,
    Count8Bit,
    Count16Bit,
    Count32Bit,
    Count64Bit
}

public enum CompareOp
{
    Never,
    Less,
    Equal,
    LessOrEqual,
    Greater,
    NotEqual,
    GreaterOrEqual,
    Always
}

public enum StencilOp
{
    Keep,
    Zero,
    Replace,
    IncrementAndClamp,
    DecrementAndClamp,
    Invert,
    IncrementAndWrap,
    DecrementAndWrap
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
    OneMinusSrc1Alpha
}

public enum BlendOp
{
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max
}

public enum ColorComponentFlags
{
    None,
    RBit,
    GBit,
    BBit,
    ABit
}

public enum LogicOp
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
    Set
}

public enum ImageLayout
{
    Undefined,
    General,
    ColorAttachmentOptimal,
    DepthStencilAttachmentOptimal,
    DepthStencilReadOnlyOptimal,
    ShaderReadOnlyOptimal,
    TransferSrcOptimal,
    TransferDstOptimal,
    Preinitialized,
    Present
}

public enum ShaderStage
{
    Vertex,
    Pixel,
    Compute,
    Count
}

public enum BufferUsage
{
    None,
    TransferSrc,
    TransferDst,
    UniformTexel,
    StorageTexel,
    Uniform,
    Storage,
    Index,
    Vertex,
    Indirect
}

public enum SharingMode
{
    Exclusive,
    Concurrent
}

public static class Constants
{
    public const int MaxFramesInFlight = 2;
}

public struct Viewport
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float MinDepth { get; set; }
    public float MaxDepth { get; set; }
}

public struct Rect2D
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
