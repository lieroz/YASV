namespace YASV.RHI;

public class BufferDesc
{
    public int Size { get; set; }
    public BufferUsage[] Usages { get; set; } = [BufferUsage.None];
    public SharingMode SharingMode { get; set; }
}

public class Buffer(int size)
{
    public int Size { get; } = size;
}
