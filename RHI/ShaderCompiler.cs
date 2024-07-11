namespace YASV.RHI;

public abstract class ShaderCompiler
{
    public abstract byte[] Compile(string path, ShaderStage shaderStage, bool useSpirv);
}
