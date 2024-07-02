using System;
using Silk.NET.Vulkan;

namespace YASV.RHI;

internal class VulkanShaderWrapper(ShaderModule shaderModule, Shader.Stage stage) : Shader(stage)
{
    public readonly ShaderModule _shaderModule = shaderModule;
}

internal static class VulkanShaderExtensions
{
    internal static ShaderModule ToVulkanShader(this Shader shader)
    {
        return ((VulkanShaderWrapper)shader)._shaderModule;
    }

    internal static ShaderStageFlags ToVulkanShaderStage(this Shader.Stage stage)
    {
        return stage switch
        {
            Shader.Stage.Vertex => ShaderStageFlags.VertexBit,
            Shader.Stage.Fragment => ShaderStageFlags.FragmentBit,
            _ => throw new NotSupportedException($"Shader stage '{stage}' is not supported.")
        };
    }
}
