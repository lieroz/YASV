namespace YASV.RHI;

public class Shader(Shader.Stage stage)
{
    public enum Stage
    {
        Vertex,
        Fragment,
        Count
    }

    public readonly Stage _stage = stage;
}
