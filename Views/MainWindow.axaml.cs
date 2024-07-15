using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using YASV.Scenes;
using YASV.ViewModels;

namespace YASV.Views;

public partial class MainWindow : Window
{
    private readonly List<Type> _sceneTypes = Helpers.GetSceneTypes();
    private readonly SilkNETWindow? _renderWindow;

    public MainWindow()
    {
        InitializeComponent();

        scenes.ItemsSource = _sceneTypes.Select(type => type.Name);
        _renderWindow = this.GetControl<SilkNETWindow>("SilkNETWindow");

        scenes.SelectionChanged += OnSelectionChanged;
    }

    public void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _renderWindow!.EnqueueAction(() =>
        {
            _renderWindow!.GraphicsDevice.WaitIdle();
            _renderWindow.CurrentScene?.Dispose();
            _renderWindow.CurrentScene = (BaseScene)Activator.CreateInstance(_sceneTypes[scenes.SelectedIndex], _renderWindow.GraphicsDevice)!;
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderWindow!.Dispose();
        base.OnClosed(e);
    }
}
