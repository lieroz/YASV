namespace YASV.RHI;

// TODO: Add more shader stages
public enum ShaderStage
{
    Vertex,
    Pixel
}

public abstract class ShaderCompiler
{
    public abstract byte[] Compile(string path, ShaderStage shaderStage, bool useSpirv);
}
