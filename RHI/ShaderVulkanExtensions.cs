using Silk.NET.Vulkan;

namespace YASV.RHI;

internal class VulkanShaderWrapper(ShaderModule shaderModule, ShaderStage stage) : Shader(stage)
{
    public ShaderModule ShaderModule { get; private set; } = shaderModule;
}

internal static class VulkanShaderExtensions
{
    internal static ShaderModule ToVulkanShader(this Shader shader)
    {
        return ((VulkanShaderWrapper)shader).ShaderModule;
    }

    internal static ShaderStageFlags ToVulkanShaderStage(this ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => ShaderStageFlags.VertexBit,
            ShaderStage.Pixel => ShaderStageFlags.FragmentBit,
            ShaderStage.Compute => ShaderStageFlags.ComputeBit,
            _ => throw new NotSupportedException($"Shader stage '{stage}' is not supported.")
        };
    }
}
