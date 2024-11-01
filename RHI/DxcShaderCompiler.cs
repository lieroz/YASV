using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;

namespace YASV.RHI;

public class DxcShaderCompiler : ShaderCompiler
{
    private const string TargetProfileVersion = "6_5";
    private readonly DXC _dxc;
    private ComPtr<IDxcUtils> _dxcUtils;
    private ComPtr<IDxcCompiler3> _dxcCompiler;

    public unsafe DxcShaderCompiler()
    {
        var dxcLibrary = "dxcompiler";
#if DEBUG
        dxcLibrary += "d";
#endif

        if (OperatingSystem.IsWindows())
        {
            _dxc = new DXC(DXC.CreateDefaultContext([$"./Libraries/Native/Windows/Dxc/{dxcLibrary}.dll"]));
        }
        else
        {
            throw new PlatformNotSupportedException($"Unsupporeted platform: {Environment.OSVersion.Platform}");
        }

        // TODO: Silk.NET should add CLSID_ guids
        {
            Guid rclsid = new Guid("6245D6AF-66E0-48FD-80B4-4D271796748C");
            _dxcUtils = _dxc.CreateInstance<IDxcUtils>(&rclsid);
        }
        {
            Guid rclsid = new Guid("73E22D93-E6CE-47F3-B5BF-F0664F39C1B0");
            _dxcCompiler = _dxc.CreateInstance<IDxcCompiler3>(&rclsid);
        }
    }

    // TODO: -Qstrip_debug, -Qstrip_reflect to separate structure
    public override unsafe byte[] Compile(string path, ShaderStage shaderStage, bool useSpirv)
    {
        ComPtr<IDxcBlobEncoding> shaderBlob = new();
        SilkMarshal.ThrowHResult(_dxcUtils.LoadFile(path, null, ref shaderBlob));

        Silk.NET.Direct3D.Compilers.Buffer buffer = new()
        {
            Encoding = DXC.CPAcp,
            Ptr = shaderBlob.GetBufferPointer(),
            Size = shaderBlob.GetBufferSize()
        };

        string targetProfile = shaderStage switch
        {
            ShaderStage.Vertex => $"vs",
            ShaderStage.Pixel => $"ps",
            _ => throw new NotSupportedException($"Shader stage not supported: {shaderStage}."),
        };
        targetProfile = $"{targetProfile}_{TargetProfileVersion}";

        string[] args = ["-E", "main", "-T", targetProfile, "-WX", "-Zi", "-I", "./Shaders"];
        if (useSpirv)
        {
            args = [.. args, "-spirv"];
        }

        ComPtr<IDxcIncludeHandler> includeHandler = new();
        SilkMarshal.ThrowHResult(_dxcUtils.CreateDefaultIncludeHandler(ref includeHandler));

        ComPtr<IDxcResult> compileResult = new();
        {
            // TODO: Silk.NET should force LPWStr encoding for args for all dxc compiler Compile methods
            var pArgs = SilkMarshal.StringArrayToPtr(args, NativeStringEncoding.LPWStr);
            SilkMarshal.ThrowHResult(_dxcCompiler.Compile(in buffer,
                (char**)pArgs,
                (uint)args.Length,
                includeHandler,
                SilkMarshal.GuidPtrOf<IDxcResult>(),
                (void**)compileResult.GetAddressOf())
            );
            SilkMarshal.Free(pArgs);
        }

        ComPtr<IDxcBlobUtf16> outputName = new();

        ComPtr<IDxcBlobUtf8> errors = new();
        compileResult.GetOutput(OutKind.Errors, ref errors, ref outputName);

        if (errors.Handle != null && errors.GetStringLength() > 0)
        {
            throw new ArgumentException(errors.GetStringPointerS());
        }

        ComPtr<IDxcBlob> objectBlob = new();
        SilkMarshal.ThrowHResult(compileResult.GetOutput(OutKind.Object, ref objectBlob, ref outputName));

        var resultBuffer = new byte[objectBlob.GetBufferSize()];
        Marshal.Copy((nint)objectBlob.GetBufferPointer(), resultBuffer, 0, (int)objectBlob.GetBufferSize());

        return resultBuffer;
    }
}
