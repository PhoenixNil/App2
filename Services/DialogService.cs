using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace App2.Services;

/// <summary>
/// WinUI ContentDialog 的默认实现。
/// </summary>
public class DialogService : IDialogService
{
	private readonly IWindowContext _windowContext;
	private readonly IClipboardService _clipboardService;
	private readonly IThemeService _themeService;

	public DialogService(IWindowContext windowContext, IClipboardService clipboardService, IThemeService themeService)
	{
		_windowContext = windowContext;
		_clipboardService = clipboardService;
		_themeService = themeService;
	}

	public async Task ShowMessageAsync(string title, string message, string closeButtonText = "确定")
	{
		var dialog = CreateDialog(title, message);
		dialog.CloseButtonText = closeButtonText;
		await dialog.ShowAsync();
	}

	public Task ShowErrorAsync(string title, string message)
	{
		return ShowMessageAsync(title, message);
	}

	public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "确定", string cancelText = "取消", bool isDanger = false)
	{
		var dialog = CreateDialog(title, message);
		dialog.PrimaryButtonText = confirmText;
		dialog.CloseButtonText = cancelText;
		dialog.DefaultButton = isDanger ? ContentDialogButton.None : ContentDialogButton.Primary;
		if (isDanger && Application.Current.Resources.TryGetValue("DangerAccentButtonStyle", out var style) && style is Style buttonStyle)
		{
			dialog.PrimaryButtonStyle = buttonStyle;
		}

		var result = await dialog.ShowAsync();
		return result == ContentDialogResult.Primary;
	}

	public async Task<string?> PromptForTextAsync(string title, string placeholder, string? description = null, bool multiline = false)
	{
		var textBox = new TextBox
		{
			PlaceholderText = placeholder,
			AcceptsReturn = multiline,
			TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
			Height = multiline ? 120 : double.NaN
		};

		var panel = new StackPanel { Spacing = 8 };
		if (!string.IsNullOrWhiteSpace(description))
		{
			panel.Children.Add(new TextBlock
			{
				Text = description,
				Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
				TextWrapping = TextWrapping.Wrap
			});
		}
		panel.Children.Add(textBox);

		var errorText = new TextBlock
		{
			Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
			Visibility = Visibility.Collapsed,
			TextWrapping = TextWrapping.Wrap
		};
		panel.Children.Add(errorText);

		var dialog = CreateDialog(title, panel);
		dialog.PrimaryButtonText = "确定";
		dialog.CloseButtonText = "取消";
		dialog.DefaultButton = ContentDialogButton.Primary;

		dialog.PrimaryButtonClick += (_, args) =>
		{
			if (string.IsNullOrWhiteSpace(textBox.Text))
			{
				errorText.Text = "请输入内容";
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
			}
		};

		var result = await dialog.ShowAsync();
		return result == ContentDialogResult.Primary ? textBox.Text.Trim() : null;
	}

	public async Task<ServerDialogResult?> ShowServerEditorAsync(
		string title,
		ServerDialogResult? defaults,
		IReadOnlyList<string> methodOptions,
		Func<ServerDialogResult, string?>? validate = null)
	{
		var nameBox = new TextBox { PlaceholderText = "别名", Text = defaults?.Name ?? string.Empty };
		var hostBox = new TextBox { PlaceholderText = "服务器地址", Text = defaults?.Host ?? string.Empty };
		var portBox = new TextBox { PlaceholderText = "端口", Text = defaults?.Port ?? string.Empty };
		var passwordBox = new TextBox { PlaceholderText = "密码", Text = defaults?.Password ?? string.Empty };

		var methodCombo = new ComboBox
		{
			ItemsSource = methodOptions,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			PlaceholderText = "加密方式"
		};
		if (!string.IsNullOrEmpty(defaults?.Method) && methodOptions.Contains(defaults.Method))
		{
			methodCombo.SelectedItem = defaults.Method;
		}

		var hintText = new TextBlock
		{
			Text = "注意：SS2022 密钥需要符合 Base64 长度要求。",
			FontSize = 11,
			Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
			TextWrapping = TextWrapping.Wrap
		};

		var errorText = new TextBlock
		{
			Visibility = Visibility.Collapsed,
			Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
			TextWrapping = TextWrapping.Wrap
		};

		var panel = new StackPanel { Spacing = 8 };
		panel.Children.Add(nameBox);
		panel.Children.Add(hostBox);
		panel.Children.Add(portBox);
		panel.Children.Add(passwordBox);
		panel.Children.Add(methodCombo);
		panel.Children.Add(hintText);
		panel.Children.Add(errorText);

		var dialog = CreateDialog(title, panel);
		dialog.PrimaryButtonText = "保存";
		dialog.CloseButtonText = "取消";
		dialog.DefaultButton = ContentDialogButton.Primary;

		ServerDialogResult? result = null;

		dialog.PrimaryButtonClick += (_, args) =>
		{
			var candidate = new ServerDialogResult
			{
				Name = nameBox.Text?.Trim() ?? string.Empty,
				Host = hostBox.Text?.Trim() ?? string.Empty,
				Port = portBox.Text?.Trim() ?? string.Empty,
				Password = passwordBox.Text?.Trim() ?? string.Empty,
				Method = methodCombo.SelectedItem as string ?? defaults?.Method ?? (methodOptions.Count > 0 ? methodOptions[0] : string.Empty)
			};

			if (string.IsNullOrWhiteSpace(candidate.Host) || string.IsNullOrWhiteSpace(candidate.Port))
			{
				errorText.Text = "服务器地址和端口不能为空。";
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
				return;
			}

			var validationMessage = validate?.Invoke(candidate);
			if (!string.IsNullOrEmpty(validationMessage))
			{
				errorText.Text = validationMessage;
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
				return;
			}

			result = candidate;
		};

		var dialogResult = await dialog.ShowAsync();
		return dialogResult == ContentDialogResult.Primary ? result : null;
	}

	public async Task<int?> ShowLocalPortDialogAsync(int currentPort, int minPort, int maxPort)
	{
		var numberBox = new NumberBox
		{
			Header = "本地端口",
			Minimum = minPort,
			Maximum = maxPort,
			Value = currentPort,
			ValidationMode = NumberBoxValidationMode.Disabled,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden
		};

		var rangeText = new TextBlock
		{
			Text = $"有效范围：{minPort} - {maxPort}",
			Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
			TextWrapping = TextWrapping.Wrap
		};

		var errorText = new TextBlock
		{
			Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
			Visibility = Visibility.Collapsed,
			TextWrapping = TextWrapping.Wrap
		};

		var panel = new StackPanel { Spacing = 8 };
		panel.Children.Add(numberBox);
		panel.Children.Add(rangeText);
		panel.Children.Add(errorText);

		var dialog = CreateDialog("编辑本地端口", panel);
		dialog.PrimaryButtonText = "确定";
		dialog.CloseButtonText = "取消";
		dialog.DefaultButton = ContentDialogButton.Primary;

		int? result = null;
		dialog.PrimaryButtonClick += (_, args) =>
		{
			var value = numberBox.Value;
			if (double.IsNaN(value))
			{
				errorText.Text = "请输入有效的端口号。";
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
				return;
			}

			var port = (int)Math.Round(value);
			if (port < minPort || port > maxPort)
			{
				errorText.Text = $"端口号必须在 {minPort} 到 {maxPort} 之间。";
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
				return;
			}

			result = port;
		};

		var dialogResult = await dialog.ShowAsync();
		return dialogResult == ContentDialogResult.Primary ? result : null;
	}

	public Task ShowPortChangedReminderAsync()
	{
		return ShowMessageAsync("提示", "端口号已更新，停止并重新启动服务后生效。", "知道了");
	}

	public async Task ShowLogsAsync(string logText, bool allowCopy)
	{
		var textBlock = new TextBlock
		{
			Text = logText,
			TextWrapping = TextWrapping.Wrap,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 13
		};

		var scrollViewer = new ScrollViewer
		{
			Content = textBlock,
			MaxHeight = 320,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};

		var dialog = CreateDialog("近期日志", scrollViewer);
		dialog.CloseButtonText = "关闭";
		dialog.DefaultButton = ContentDialogButton.Close;

		if (allowCopy)
		{
			dialog.PrimaryButtonText = "复制全部";
			dialog.DefaultButton = ContentDialogButton.Primary;
			dialog.PrimaryButtonClick += (_, _) => _clipboardService.SetText(logText);
		}

		await dialog.ShowAsync();
	}

	private ContentDialog CreateDialog(string title, object content)
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = content,
			XamlRoot = _windowContext.XamlRoot,
			RequestedTheme = _themeService.ActualTheme
		};

		if (dialog.XamlRoot == null)
		{
			throw new InvalidOperationException("DialogService 需要先调用 IWindowContext.Attach 才能使用。");
		}

		return dialog;
	}
}
