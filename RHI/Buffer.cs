namespace YASV.RHI;

public class BufferDesc
{
    public int Size { get; set; }
    public BufferUsage Usage { get; set; }
    public SharingMode SharingMode { get; set; }
}

public class Buffer(int size)
{
    public int Size { get; } = size;
}
