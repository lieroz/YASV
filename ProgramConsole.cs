using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using YASV.Helpers;
using YASV.RHI;
using YASV.Scenes;

namespace YASV;

internal sealed class ProgramConsole
{
    private static BaseScene? _scene;
    private static List<Tuple<bool, Vector2>>? _miceStates;
    private static List<Tuple<IKeyboard, bool, Key>>? _keyboardsStates;

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

    private static void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        _keyboardsStates![keyboard.Index] = new(keyboard, true, key);
        switch (key)
        {
            case Key.W:
                _scene!.Camera.ProcessKeyboard(Camera.Direction.Forward);
                break;
            case Key.A:
                _scene!.Camera.ProcessKeyboard(Camera.Direction.Left);
                break;
            case Key.S:
                _scene!.Camera.ProcessKeyboard(Camera.Direction.Backward);
                break;
            case Key.D:
                _scene!.Camera.ProcessKeyboard(Camera.Direction.Right);
                break;
        }
    }

    private static void KeyUp(IKeyboard keyboard, Key key, int keyCode)
    {
        _keyboardsStates![keyboard.Index] = new(keyboard, false, Key.Unknown);
    }

    private static void MouseDown(IMouse mouse, MouseButton button)
    {
        _miceStates![mouse.Index] = new(true, mouse.Position);
    }

    private static void MouseMove(IMouse mouse, Vector2 position)
    {
        var (pressed, prevPos) = _miceStates![mouse.Index];
        if (pressed)
        {
            var offset = position - prevPos;
            _miceStates[mouse.Index] = new(pressed, position);
            _scene?.Camera.ProcessMouseMotion(offset.X, offset.Y);
        }
    }

    private static void MouseUp(IMouse mouse, MouseButton button)
    {
        _miceStates![mouse.Index] = new(false, mouse.Position);
    }

    public static void Main()
    {
        var vkApi = VulkanHelpers.GetApi();
        var window = CreateWindow();
        var graphicsDevice = new VulkanDevice(vkApi, window);
        graphicsDevice.Create(Sdl.GetApi());

        var inputContext = window.CreateInput();

        _keyboardsStates = new List<Tuple<IKeyboard, bool, Key>>(inputContext.Keyboards.Count);
        for (int i = 0; i < inputContext.Keyboards.Count; i++)
        {
            _keyboardsStates.Add(new(inputContext.Keyboards[i], false, Key.Unknown));
            inputContext.Keyboards[i].KeyDown += KeyDown;
            inputContext.Keyboards[i].KeyUp += KeyUp;
        }

        _miceStates = new List<Tuple<bool, Vector2>>(inputContext.Mice.Count);
        for (int i = 0; i < inputContext.Mice.Count; i++)
        {
            _miceStates.Add(new(false, new()));
            inputContext.Mice[i].MouseDown += MouseDown;
            inputContext.Mice[i].MouseMove += MouseMove;
            inputContext.Mice[i].MouseUp += MouseUp;
        }

        var sceneTypes = ReflectionHelpers.GetSceneTypes();
        for (int i = 0; i < sceneTypes.Count; i++)
        {
            Console.WriteLine($"{i}. {sceneTypes[i].Name}");
        }

        Console.WriteLine($"Choose scene: ");
        var key = Console.ReadLine();
        var sceneIndex = int.Parse(key!);

        _scene = (BaseScene)Activator.CreateInstance(sceneTypes[sceneIndex], graphicsDevice)!;

        window.Render += (double delta) =>
        {
            _scene.DrawScene();
        };

        double eps = 0;
        window.Update += (double delta) =>
        {
            eps += delta;
            for (int i = 0; i < _keyboardsStates.Count; i++)
            {
                var (keyboard, pressed, key) = _keyboardsStates[i];
                if (pressed && eps > 0.025)
                {
                    KeyDown(keyboard, key, -1);
                    eps = 0;
                }
            }
        };

        window.Run();
        graphicsDevice.WaitIdle();

        _scene.Dispose();

        graphicsDevice.Destroy();
    }
}
