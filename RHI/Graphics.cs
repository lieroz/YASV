namespace YASV.RHI;

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

public static class Constants
{
    public const int MaxFramesInFlight = 2;
}