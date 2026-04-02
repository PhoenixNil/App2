using App2.Controls;
using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace App2.Views;

public sealed partial class ServerDetailControl : UserControl
{
	public ServerDetailControl()
	{
		InitializeComponent();
	}

	public ServerDetailViewModel? ViewModel
	{
		get => (ServerDetailViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(ServerDetailViewModel), typeof(ServerDetailControl), new PropertyMetadata(null));

	private void ShareCopyButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not CopyButton copyButton || string.IsNullOrWhiteSpace(copyButton.TextToCopy))
		{
			return;
		}
		
	}


}
