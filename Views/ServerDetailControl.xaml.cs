using App2.Controls;
using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Diagnostics;

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

		try
		{
			if (!AppNotificationManager.IsSupported())
			{
				return;
			}

			var notification = new AppNotificationBuilder()
				.AddText("\u0053\u0053 \u94FE\u63A5\u5DF2\u590D\u5236")
				.AddText("\u5F53\u524D\u8282\u70B9\u7684 \u0053\u0053 \u94FE\u63A5\u5DF2\u590D\u5236\u5230\u526A\u8D34\u677F\u3002")
				.BuildNotification();

			AppNotificationManager.Default.Show(notification);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to show copy notification: {ex.Message}");
		}
	}
}
