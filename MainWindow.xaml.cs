using App2.Models;
using App2.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WinRT.Interop;
using System.IO;
using WinUIEx;

namespace App2;

public sealed partial class MainWindow : Window
{
	public MainWindowViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();

		// Initialize ViewModel
		ViewModel = new MainWindowViewModel();

		// Apply theme
		ApplyTheme(ViewModel.CurrentTheme);

		// Set window default size (DIP 950x600), considering system DPI scaling
		var hWnd = WindowNative.GetWindowHandle(this);
		var id = Win32Interop.GetWindowIdFromWindow(hWnd);
		var appWindow = AppWindow.GetFromWindowId(id);
		var dpiScale = GetWindowScale(hWnd);
		var widthPx = (int)Math.Round(950 * dpiScale);
		var heightPx = (int)Math.Round(600 * dpiScale);
		appWindow.Resize(new SizeInt32(widthPx, heightPx));

		//Set taskbar Icon;
		string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "output.ico");
		appWindow.SetIcon(iconPath);
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(AppTitleBar);

		//配置系统托盘（通过WindowManager，不需要继承WindowEx）
		SetupTrayIcon();

		Closed += MainWindow_Closed;

		// Initialize ViewModel after window is loaded, passing DispatcherQueue
		ViewModel.Initialize(DispatcherQueue);
	}

	private void SetupTrayIcon()
	{
		var wm = WindowManager.Get(this);
		
		// 显示系统托盘图标
		wm.IsVisibleInTray = true;
		var appWindow = wm.AppWindow;
		appWindow.Closing += (sender, args) =>
		{
			// 取消关闭操作
			args.Cancel = true;

			// 最小化并隐藏窗口（不在任务切换器中显示）
			wm.WindowState = WinUIEx.WindowState.Minimized;
			appWindow.Hide();
		};

		// 添加系统托盘右键菜单
		wm.TrayIconContextMenu += (w, e) =>
		{
			var flyout = new MenuFlyout();
			
			var openItem = new MenuFlyoutItem { Text = "打开窗口" };
			openItem.Click += (s, args) => 
			{
				Activate();
				wm.WindowState = WinUIEx.WindowState.Normal;
			};
			flyout.Items.Add(openItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			var exitItem = new MenuFlyoutItem { Text = "退出" };
			exitItem.Click += (s, args) => Close();
			flyout.Items.Add(exitItem);

			e.Flyout = flyout;
		};
	}

	private async void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		await ViewModel.CleanupAsync();
	}

	#region UI Event Handlers - Import SS Link

	private async void BtnImportSS_Click(object sender, RoutedEventArgs e)
	{
		var tbSSUrl = new TextBox
		{
			PlaceholderText = "粘贴 SS 链接 (ss://...)",
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			Height = 120
		};

		var hint = new TextBlock
		{
			Text = "支持标准 ss:// 链接。",
			FontSize = 12,
			Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
		};

		var stack = new StackPanel { Spacing = 8 };
		stack.Children.Add(hint);
		stack.Children.Add(tbSSUrl);

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = "从 SS 链接导入",
			Content = stack,
			PrimaryButtonText = "导入",
			CloseButtonText = "取消",
			DefaultButton = ContentDialogButton.Primary
		};

		dialog.PrimaryButtonClick += (_, args) =>
		{
			var ssUrl = tbSSUrl.Text.Trim();
			if (string.IsNullOrWhiteSpace(ssUrl))
			{
				args.Cancel = true;
				tbSSUrl.Focus(FocusState.Programmatic);
				return;
			}

			var entry = ViewModel.ParseSSUrl(ssUrl);
			if (entry is null)
			{
				args.Cancel = true;
				tbSSUrl.Focus(FocusState.Programmatic);
				return;
			}

			dialog.Tag = entry;
		};

		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
		{
			ViewModel.AddServer(entry);
		}
	}

	#endregion

	#region UI Event Handlers - Manual Add/Edit/Remove Server

	private async void BtnAddManual_Click(object sender, RoutedEventArgs e)
	{
		var dialog = CreateServerDialog("手动添加服务器", null);
		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
		{
			ViewModel.AddServer(entry);
		}
	}

	private async void BtnEdit_Click(object sender, RoutedEventArgs e)
	{
		if (ViewModel.SelectedServer == null)
		{
			return;
		}

		var dialog = CreateServerDialog("编辑服务器", ViewModel.SelectedServer.Clone());
		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
		{
			ViewModel.UpdateServer(ViewModel.SelectedServer, entry);
		}
	}

	private async void BtnRemove_Click(object sender, RoutedEventArgs e)
	{
		if (ViewModel.SelectedServer == null)
		{
			return;
		}

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = "确认删除",
			Content = $"确定要删除 {ViewModel.SelectedServer.Name}?",
			PrimaryButtonText = "删除",
			CloseButtonText = "取消",
			PrimaryButtonStyle = (Style)Application.Current.Resources["DangerAccentButtonStyle"],
			DefaultButton = ContentDialogButton.None
		};

		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary)
		{
			ViewModel.RemoveServer(ViewModel.SelectedServer);
		}
	}

	private ContentDialog CreateServerDialog(string title, ServerEntry? existing)
	{
		var tbName = new TextBox { PlaceholderText = "别名", Text = existing?.Name ?? string.Empty };
		var tbHost = new TextBox { PlaceholderText = "服务器地址", Text = existing?.Host ?? string.Empty };
		var tbPort = new TextBox { PlaceholderText = "端口", Text = existing?.Port.ToString() ?? string.Empty };
		var tbPassword = new TextBox { PlaceholderText = "密码", Text = existing?.Password ?? string.Empty };

		var cbMethod = new ComboBox
		{
			ItemsSource = new[]
			{
				"aes-128-gcm",
				"aes-256-gcm",
				"chacha20-ietf-poly1305",
				"2022-blake3-aes-256-gcm",
				"2022-blake3-aes-128-gcm",
				"2022-blake3-chacha20-poly1305"
			},
			PlaceholderText = "加密方式",
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		if (existing is null)
		{
			cbMethod.SelectedIndex = -1;
		}
		else
		{
			cbMethod.SelectedItem = existing.Method;
		}

		var hintText = new TextBlock
		{
			Text = "注意：SS2022 密钥需要符合 Base64 长度要求。",
			FontSize = 11,
			Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
			TextWrapping = TextWrapping.Wrap
		};

		var stack = new StackPanel { Spacing = 8 };
		stack.Children.Add(tbName);
		stack.Children.Add(tbHost);
		stack.Children.Add(tbPort);
		stack.Children.Add(tbPassword);
		stack.Children.Add(cbMethod);
		stack.Children.Add(hintText);

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = title,
			Content = stack,
			PrimaryButtonText = "保存",
			CloseButtonText = "取消",
			DefaultButton = ContentDialogButton.Primary
		};

		dialog.PrimaryButtonClick += (_, args) =>
		{
			try
			{
				var method = cbMethod.SelectedItem as string ?? "aes-256-gcm";
				var entry = ViewModel.CreateServerEntry(
					tbName.Text,
					tbHost.Text,
					tbPort.Text,
					tbPassword.Text,
					method);

				dialog.Tag = entry;
			}
			catch (ArgumentException)
			{
				args.Cancel = true;
				// Focus on the problematic field
				if (string.IsNullOrWhiteSpace(tbHost.Text))
				{
					tbHost.Focus(FocusState.Programmatic);
				}
				else
				{
					tbPort.Focus(FocusState.Programmatic);
				}
			}
		};

		return dialog;
	}

	#endregion

	#region UI Event Handlers - Server Search

	private void ServerSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
		{
			return;
		}

		var query = sender.Text.Trim();
		if (string.IsNullOrEmpty(query))
		{
			sender.ItemsSource = null;
			return;
		}

		var matches = ViewModel.Servers
			.Where(server => !string.IsNullOrEmpty(server.Name) &&
				server.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
			.ToList();

		sender.ItemsSource = matches;
	}

	private void ServerSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		if (args.SelectedItem is ServerEntry server)
		{
			ViewModel.SelectedServer = server;
			sender.Text = server.Name;
		}
	}

	private void ServerSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (args.ChosenSuggestion is ServerEntry chosenServer)
		{
			ViewModel.SelectedServer = chosenServer;
			return;
		}

		var query = args.QueryText?.Trim();
		if (string.IsNullOrEmpty(query))
		{
			return;
		}

		var match = ViewModel.Servers.FirstOrDefault(server =>
			string.Equals(server.Name, query, StringComparison.OrdinalIgnoreCase));

		match ??= ViewModel.Servers.FirstOrDefault(server =>
			!string.IsNullOrEmpty(server.Name) &&
			server.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

		if (match != null)
		{
			ViewModel.SelectedServer = match;
			sender.Text = match.Name;
		}
	}

	#endregion

	#region UI Event Handlers - Start/Stop

	private async void Button_Click(object sender, RoutedEventArgs e)
	{
		await BtnStartStop_ClickAsync();
	}

	private async Task BtnStartStop_ClickAsync()
	{
		if (!ViewModel.IsRunning && ViewModel.SelectedServer == null)
		{
			var dialog = new ContentDialog
			{
				XamlRoot = Content.XamlRoot,
				Title = "未选择服务器",
				Content = "请先选择一个服务器节点",
				CloseButtonText = "确定"
			};

			await dialog.ShowAsync();
			BtnStartStop.IsChecked = ViewModel.IsRunning;
			return;
		}

		try
		{
			await ViewModel.StartStopCommand.ExecuteAsync(null);
		}
		catch (Exception ex)
		{
			var dialog = new ContentDialog
			{
				XamlRoot = Content.XamlRoot,
				Title = "启动失败",
				Content = ex.Message,
				CloseButtonText = "确定"
			};

			await dialog.ShowAsync();
		}

		BtnStartStop.IsChecked = ViewModel.StartStopButtonChecked;
	}

	#endregion

	#region UI Event Handlers - Settings Menu

	private async void EditLocalPortMenuItem_Click(object sender, RoutedEventArgs e)
	{
		var numberBox = new NumberBox
		{
			Header = "本地端口",
			Minimum = 1024,
			Maximum = 65535,
			Value = int.Parse(ViewModel.LocalPortText),
			ValidationMode = NumberBoxValidationMode.Disabled,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden
		};

		var rangeText = new TextBlock
		{
			Text = "有效范围：1024 - 65535",
			Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
			TextWrapping = TextWrapping.Wrap
		};

		var errorText = new TextBlock
		{
			Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
			Visibility = Visibility.Collapsed,
			TextWrapping = TextWrapping.Wrap
		};

		var contentPanel = new StackPanel { Spacing = 8 };
		contentPanel.Children.Add(numberBox);
		contentPanel.Children.Add(rangeText);
		contentPanel.Children.Add(errorText);

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = "编辑本地端口",
			PrimaryButtonText = "确定",
			CloseButtonText = "取消",
			DefaultButton = ContentDialogButton.Primary,
			Content = contentPanel
		};

		var portChanged = false;

		dialog.PrimaryButtonClick += (_, args) =>
		{
			if (ViewModel.ValidateAndUpdateLocalPort(numberBox.Value, out var errorMessage))
			{
				portChanged = true;
			}
			else if (errorMessage != null)
			{
				errorText.Text = errorMessage;
				errorText.Visibility = Visibility.Visible;
				args.Cancel = true;
			}
		};

		await dialog.ShowAsync();

		if (portChanged && ViewModel.IsRunning)
		{
			var reminderDialog = new ContentDialog
			{
				XamlRoot = Content.XamlRoot,
				Title = "提示",
				Content = "端口号已更新，停止并重新启动服务后生效。",
				CloseButtonText = "知道了"
			};

			await reminderDialog.ShowAsync();
		}
	}

	private async void ViewLogsMenuItem_Click(object sender, RoutedEventArgs e)
	{
		var logText = ViewModel.GetLogsText();
		var hasLogs = ViewModel.HasLogs;

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

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = "近期日志",
			Content = scrollViewer,
			CloseButtonText = "关闭",
			DefaultButton = ContentDialogButton.Close
		};

		if (hasLogs)
		{
			dialog.PrimaryButtonText = "复制全部";
			dialog.DefaultButton = ContentDialogButton.Primary;
			dialog.PrimaryButtonClick += (_, _) =>
			{
				var dataPackage = new DataPackage();
				dataPackage.SetText(logText);
				Clipboard.SetContent(dataPackage);
				Clipboard.Flush();
			};
		}

		await dialog.ShowAsync();
	}

	private void TestButton2Click(object sender, RoutedEventArgs e)
	{
		TestButton2TeachingTip.IsOpen = true;
	}

	#endregion

	#region Theme Management

	private void ThemeButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not string tag)
		{
			return;
		}

		if (!Enum.TryParse<ElementTheme>(tag, true, out var theme))
		{
			return;
		}

		ApplyTheme(theme);
		ViewModel.ApplyTheme(theme);
		TestButton2TeachingTip.IsOpen = false;
	}

	private void ApplyTheme(ElementTheme theme)
	{
		if (Content is FrameworkElement root)
		{
			root.RequestedTheme = theme;
		}

		UpdateThemeButtonsState();
	}

	private void UpdateThemeButtonsState()
	{
		if (TestButton2TeachingTip.Content is not StackPanel panel)
		{
			return;
		}

		foreach (var child in panel.Children)
		{
			if (child is Button themeButton && themeButton.Tag is string tag &&
				Enum.TryParse<ElementTheme>(tag, true, out var theme))
			{
				themeButton.IsEnabled = theme != ViewModel.CurrentTheme;
			}
		}
	}

	#endregion

	#region Helper Methods

	private static double GetWindowScale(IntPtr hwnd)
	{
		try
		{
			if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
			{
				var dpi = GetDpiForWindow(hwnd);
				if (dpi > 0)
				{
					return dpi / 96.0;
				}
			}
		}
		catch
		{
			// ignore and fallback
		}

		return 1.0;
	}

	[DllImport("user32.dll")]
	private static extern int GetDpiForWindow(IntPtr hWnd);

	#endregion
}

// Extension method for executing async commands
public static class CommandExtensions
{
	public static async Task ExecuteAsync(this System.Windows.Input.ICommand command, object? parameter)
	{
		if (command is AsyncRelayCommand asyncCommand)
		{
			// Execute the async command and await its completion
			await asyncCommand.ExecuteAsync();
		}
		else
		{
			command.Execute(parameter);
		}
	}
}
