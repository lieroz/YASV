namespace YASV.RHI;

public abstract class ShaderCompiler
{
    public abstract byte[] Compile(string path, Shader.Stage shaderStage, bool useSpirv);
}
