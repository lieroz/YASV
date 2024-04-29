using System;

namespace YASV.Scenes;

[Scene]
public class TriangleScene : IScene
{
    public void Draw()
    {
        Console.WriteLine("TriangleScene");
    }
}
