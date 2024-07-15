namespace YASV.RHI;

public enum VertexInputRate
{
    Vertex,
    Instance
}

public enum Format
{
    Undefined,
    R32G32B32A32_Float,
    R32G32B32A32_Uint,
    R32G32B32A32_Sint,
    R32G32B32_Float,
    R32G32B32_Uint,
    R32G32B32_Sint,
    R16G16B16A16_Float,
    R16G16B16A16_Unorm,
    R16G16B16A16_Uint,
    R16G16B16A16_Snorm,
    R16G16B16A16_Sint,
    R32G32_Float,
    R32G32_Uint,
    R32G32_Sint,
    R10G10B10A2_Unorm,
    R10G10B10A2_Uint,
    R8G8B8A8_Unorm,
    R8G8B8A8_Unorm_SRGB,
    R8G8B8A8_Uint,
    R8G8B8A8_Snorm,
    R8G8B8A8_Sint,
    R16G16_Float,
    R16G16_Unorm,
    R16G16_Uint,
    R16G16_Snorm,
    R16G16_Sint,
    D32_Float,
    R32_Float,
    R32_Uint,
    R32_Sint,
    D24_Unorm_S8_Uint,
    R8G8_Unorm,
    R8G8_Uint,
    R8G8_Snorm,
    R8G8_Sint,
    R16_Float,
    D16_Unorm,
    R16_Unorm,
    R16_Uint,
    R16_Snorm,
    R16_Sint,
    R8_Unorm,
    R8_Uint,
    R8_Snorm,
    R8_Sint,
    A8_Unorm,
    BC1_Unorm,
    BC1_Unorm_SRGB,
    BC2_Unorm,
    BC2_Unorm_SRGB,
    BC3_Unorm,
    BC3_Unorm_SRGB,
    BC4_Unorm,
    BC4_Snorm,
    BC5_Unorm,
    BC5_Snorm,
    B5G6R5_Unorm,
    B5G5R5A1_Unorm,
    B8G8R8A8_Unorm,
    B8G8R8A8_Unorm_SRGB,
    BC6H_Ufloat16,
    BC6H_Sfloat16,
    BC7_Unorm,
    BC7_Unorm_SRGB
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

public enum IndexType
{
    Uint16,
    Uint32
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
