using System.Reflection;
using YASV.Scenes;

namespace YASV.Helpers;

public static class ReflectionHelpers
{
    public static List<Type> GetSceneTypes()
    {
        var types = new List<Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.GetCustomAttributes<SceneAttribute>(true).Any())
                {
                    types.Add(type);
                }
            }
        }
        return types;
    }
}
