using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using YASV.RHI;
using YASV.Scenes;

namespace YASV;

internal sealed class ProgramConsole
{
    public static IView CreateWindow()
    {
        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "Vulkan"
        };

        Silk.NET.Windowing.Window.PrioritizeSdl();
        var window = Silk.NET.Windowing.Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
        {
            throw new Exception("Windowing platform doesn't support Vulkan.");
        }

        return window;
    }

    public static void Main()
    {
        var window = CreateWindow();
        var graphicsDevice = new VulkanDevice(window);
        graphicsDevice.Create(Sdl.GetApi());

        var scene = new TriangleScene(graphicsDevice);

        window.Render += (double delta) =>
        {
            scene.DrawScene();
        };

        window.Run();
        graphicsDevice.WaitIdle();

        scene.Dispose();
        graphicsDevice.Destroy();
    }
}
