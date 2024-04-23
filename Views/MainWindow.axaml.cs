using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using YASV.ViewModels;

namespace YASV.Views;

public partial class MainWindow : Window
{
    private readonly SilkNETWindow? _renderWindow;
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDevTools();
#endif

        _renderWindow = this.GetControl<SilkNETWindow>("SilkNETWindow");
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderWindow!.Dispose();
        base.OnClosed(e);
    }
}
