using App2.ViewModels;
using Microsoft.UI.Xaml;

namespace App2.Views;

public sealed partial class ControlPanelControl
{
	public ControlPanelControl()
	{
		InitializeComponent();
	}

	public ControlPanelViewModel? ViewModel
	{
		get => (ControlPanelViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(ControlPanelViewModel), typeof(ControlPanelControl), new PropertyMetadata(null));
}
