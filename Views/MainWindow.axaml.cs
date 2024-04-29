using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using YASV.Scenes;
using YASV.ViewModels;

namespace YASV.Views;

public partial class MainWindow : Window
{
    private readonly List<Type> _sceneTypes = GetSceneTypes();
    private readonly SilkNETWindow? _renderWindow;

    public MainWindow()
    {
#if DEBUG
        InitializeComponent();
#else
        InitializeComponent(attachDevTools: false);
#endif

        scenes.ItemsSource = _sceneTypes.Select(type => type.Name);
        _renderWindow = this.GetControl<SilkNETWindow>("SilkNETWindow");

        scenes.SelectionChanged += OnSelectionChanged;
    }

    public void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _renderWindow!.CurrentScene = (IScene)Activator.CreateInstance(_sceneTypes[scenes.SelectedIndex])!;
    }

    private static List<Type> GetSceneTypes()
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

    protected override void OnClosed(EventArgs e)
    {
        _renderWindow!.Dispose();
        base.OnClosed(e);
    }
}
