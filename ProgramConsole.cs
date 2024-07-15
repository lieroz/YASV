using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using YASV.RHI;
using YASV.Scenes;

namespace YASV;

internal sealed class ProgramConsole
{
    private static IView CreateWindow()
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

        var sceneTypes = Helpers.GetSceneTypes();
        for (int i = 0; i < sceneTypes.Count; i++)
        {
            Console.WriteLine($"{i}. {sceneTypes[i].Name}");
        }

        Console.WriteLine($"Choose scene: ");
        var key = Console.ReadLine();
        var sceneIndex = int.Parse(key!);

        var scene = (BaseScene)Activator.CreateInstance(sceneTypes[sceneIndex], graphicsDevice)!;

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
