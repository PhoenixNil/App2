using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App2.Views;

public sealed partial class ServerDetailControl : UserControl
{
    public ServerDetailControl()
    {
        InitializeComponent();
    }

    public MainWindowViewModel? ViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainWindowViewModel),
            typeof(ServerDetailControl), new PropertyMetadata(null));
}
