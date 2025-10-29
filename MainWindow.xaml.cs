using App2.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics;
using WinRT.Interop;

namespace App2;

public sealed partial class MainWindow : Window
{
	public ObservableCollection<ServerEntry> Servers { get; } = new();

	private ServerEntry? _selectedServer;
	private ServerEntry? _activeServer;
	private bool _isRunning;

	private readonly ConfigWriter _configWriter;
	private readonly EngineService _engineService;
	private readonly ProxyService _proxyService;
	private readonly ConfigStorage _configStorage;

	private ProxyMode _currentProxyMode = ProxyMode.PAC;

	// 使用 10808 作为本地监听端口，避免与常见端口冲突
	private const int LocalPort = 10808;

	public MainWindow()
	{
		InitializeComponent();

		// 设置窗口默认大小（DIP 950x600），并考虑系统 DPI 缩放
		var hWnd = WindowNative.GetWindowHandle(this);
		var id = Win32Interop.GetWindowIdFromWindow(hWnd);
		var appWindow = AppWindow.GetFromWindowId(id);
		var dpiScale = GetWindowScale(hWnd);
		var widthPx = (int)Math.Round(950 * dpiScale);
		var heightPx = (int)Math.Round(600 * dpiScale);
		appWindow.Resize(new SizeInt32(widthPx, heightPx));

		ExtendsContentIntoTitleBar = true;
		SetTitleBar(AppTitleBar);

		_configWriter = new ConfigWriter();
		_engineService = new EngineService();
		_proxyService = new ProxyService();
		_configStorage = new ConfigStorage();

		_engineService.LogReceived += OnEngineLogReceived;

		LoadServers();
		Servers.CollectionChanged += Servers_CollectionChanged;

		ServersListView.SelectedIndex = -1;
		UpdateDetails(null);

		Closed += MainWindow_Closed;
	}
	private void OnEngineLogReceived(object? sender, string log)
	{
		DispatcherQueue.TryEnqueue(() =>
		{
			Debug.WriteLine(log);

			if (log.Contains("listening on", StringComparison.OrdinalIgnoreCase))
			{
				TxtStatus.Text = "状态：运行中";
				IconStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
			}
			//else if (log.Contains("error", StringComparison.OrdinalIgnoreCase) || log.Contains("panic", StringComparison.OrdinalIgnoreCase))
			//{
			//	TxtStatus.Text = $"状态：错误 - {log}";
			//}
		});
	}

	private void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		if (_isRunning)
		{
			_engineService.Stop();
			_proxyService.ClearProxy();
		}

		_engineService.Dispose();
		_configWriter.DeleteConfig();
	}

	private void LoadServers()
	{
		try
		{
			var servers = _configStorage.LoadServers();
			foreach (var server in servers)
			{
				Servers.Add(server);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"加载服务器列表失败: {ex.Message}");
		}
	}

	private void Servers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		try
		{
			_configStorage.SaveServers(Servers);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"保存服务器列表失败: {ex.Message}");
		}
	}

	private void ServersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_selectedServer = ServersListView.SelectedItem as ServerEntry;
		var hasSelection = _selectedServer != null;
		var canModify = hasSelection && !_selectedServer!.IsActive;

		BtnEdit.IsEnabled = canModify;
		BtnRemove.IsEnabled = canModify;

		if (!hasSelection)
		{
			if (_isRunning && (_activeServer == null || !Servers.Contains(_activeServer)))
			{
				ToggleRunning(false);
			}

			UpdateDetails(null);
			return;
		}

		UpdateDetails(_selectedServer);
	}

	private void UpdateDetails(ServerEntry? server)
	{
		if (server == null)
		{
			TxtName.Text = "未选择";
			TxtHost.Text = "未选择";
			TxtPort.Text = "未选择";
			TxtMethod.Text = "未选择";
			return;
		}

		TxtName.Text = server.Name;
		TxtHost.Text = server.Host;
		TxtPort.Text = server.Port.ToString();
		TxtMethod.Text = server.Method;
	}

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
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
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

			var entry = ParseSSUrl(ssUrl);
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
			Servers.Add(entry);
			ServersListView.SelectedItem = entry;
		}
	}

	private async void BtnAddManual_Click(object sender, RoutedEventArgs e)
	{
		var dialog = CreateServerDialog("手动添加服务器", null);
		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
		{
			Servers.Add(entry);
			ServersListView.SelectedItem = entry;
		}
	}

	private async void BtnEdit_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedServer == null)
		{
			return;
		}

		var dialog = CreateServerDialog("编辑服务器", _selectedServer.Clone());
		var result = await dialog.ShowAsync();
		if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
		{
			_selectedServer.Name = entry.Name;
			_selectedServer.Host = entry.Host;
			_selectedServer.Port = entry.Port;
			_selectedServer.Password = entry.Password;
			_selectedServer.Method = entry.Method;

			_selectedServer.OnPropertyChanged(nameof(ServerEntry.Name));
			_selectedServer.OnPropertyChanged(nameof(ServerEntry.Host));
			_selectedServer.OnPropertyChanged(nameof(ServerEntry.Port));
			_selectedServer.OnPropertyChanged(nameof(ServerEntry.Method));

			UpdateDetails(_selectedServer);
		}
	}

	private async void BtnRemove_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedServer == null)
		{
			return;
		}

		var dialog = new ContentDialog
		{
			XamlRoot = Content.XamlRoot,
			Title = "确认删除",
			Content = $"确定要删除 {_selectedServer.Name}?",
			PrimaryButtonText = "删除",
			CloseButtonText = "取消",
			PrimaryButtonStyle = (Style)Application.Current.Resources["DangerAccentButtonStyle"],
			DefaultButton = ContentDialogButton.None
		};
		//// 创建资源字典来覆盖颜色
		//var resources = new ResourceDictionary();

		//// Light 主题
		//var lightTheme = new ResourceDictionary();
		//lightTheme["AccentButtonBackground"] = new SolidColorBrush(Color.FromArgb(255, 220, 20, 60));
		//lightTheme["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 184, 17, 46));
		//lightTheme["AccentButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 139, 13, 35));
		//lightTheme["AccentButtonForeground"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
		//lightTheme["AccentButtonForegroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
		//lightTheme["AccentButtonForegroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

		//// Dark 主题
		//var darkTheme = new ResourceDictionary();
		//darkTheme["AccentButtonBackground"] = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68));
		//darkTheme["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 255, 102, 102));
		//darkTheme["AccentButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 204, 51, 51));
		//darkTheme["AccentButtonForeground"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
		//darkTheme["AccentButtonForegroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
		//darkTheme["AccentButtonForegroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

		//resources.ThemeDictionaries["Light"] = lightTheme;
		//resources.ThemeDictionaries["Dark"] = darkTheme;

		//// 直接设置到 Dialog 的 Resources
		//dialog.Resources = resources;

		//// 确保 PrimaryButton 使用 AccentButtonStyle
		//dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"];

		var result = await dialog.ShowAsync();
		if (result != ContentDialogResult.Primary)
		{
			return;
		}

		var removedIndex = Servers.IndexOf(_selectedServer);
		Servers.Remove(_selectedServer);

		if (_isRunning && _activeServer == _selectedServer)
		{
			ToggleRunning(false);
		}

		if (Servers.Count == 0)
		{
			ServersListView.SelectedIndex = -1;
			UpdateDetails(null);
		}
		else
		{
			var nextIndex = Math.Min(removedIndex, Servers.Count - 1);
			ServersListView.SelectedIndex = nextIndex;
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
			Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
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
			if (!int.TryParse(tbPort.Text.Trim(), out var portValue) || portValue <= 0 || portValue > 65535)
			{
				args.Cancel = true;
				tbPort.Focus(FocusState.Programmatic);
				return;
			}

			if (string.IsNullOrWhiteSpace(tbHost.Text))
			{
				args.Cancel = true;
				tbHost.Focus(FocusState.Programmatic);
				return;
			}

			var method = cbMethod.SelectedItem as string;
			if (string.IsNullOrWhiteSpace(method))
			{
				method = "aes-256-gcm";
			}

			dialog.Tag = new ServerEntry
			{
				Name = string.IsNullOrWhiteSpace(tbName.Text) ? "未命名节点" : tbName.Text.Trim(),
				Host = tbHost.Text.Trim(),
				Port = portValue,
				Password = tbPassword.Text.Trim(),
				Method = method.Trim()
			};
		};

		return dialog;
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		BtnStartStop_Click(sender, e);
	}

	private async void BtnStartStop_Click(object sender, RoutedEventArgs e)
	{
		if (!_isRunning)
		{
			if (_selectedServer == null)
			{
				var dialog = new ContentDialog
				{
					XamlRoot = Content.XamlRoot,
					Title = "未选择服务器",
					Content = "请先选择一个服务器节点",
					CloseButtonText = "确定"
				};

				await dialog.ShowAsync();
				BtnStartStop.IsChecked = _isRunning;
				return;
			}

			ToggleRunning(true);
		}
		else
		{
			ToggleRunning(false);
		}

		BtnStartStop.IsChecked = _isRunning;
	}

	private async void ToggleRunning(bool shouldRun)
	{
		if (shouldRun)
		{
			if (_selectedServer == null)
			{
				return;
			}

			try
			{
				_configWriter.WriteConfig(_selectedServer, LocalPort);
				var configPath = _configWriter.GetConfigPath();
				if (!File.Exists(configPath))
				{
					throw new InvalidOperationException($"配置文件生成失败: {configPath}");
				}

				_engineService.Start(configPath);
				_proxyService.SetProxyServer("127.0.0.1", LocalPort);
				_proxyService.SetProxyMode(_currentProxyMode);

				_isRunning = true;
				BtnStartStop.Content = "停止";
				TxtStatus.Text = "状态：运行中";
				// 设置 IconStatus 为绿色
				IconStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];  // 绿色

				if (_activeServer != null)
				{
					_activeServer.IsActive = false;
				}

				_selectedServer.IsActive = true;
				_activeServer = _selectedServer;

				// 🔧 添加这一行：触发 SelectionChanged 重新评估按钮状态
				ServersListView_SelectionChanged(ServersListView, null!);
			}
			catch (Exception ex)
			{
				_isRunning = false;
				BtnStartStop.IsChecked = false;
				TxtStatus.Text = $"启动失败: {ex.Message}";

				var dialog = new ContentDialog
				{
					XamlRoot = Content.XamlRoot,
					Title = "启动失败",
					Content = ex.Message,
					CloseButtonText = "确定"
				};

				await dialog.ShowAsync();
			}
		}
		else
		{
			try
			{
				_engineService.Stop();
				_proxyService.ClearProxy();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"停止引擎失败: {ex.Message}");
			}
			finally
			{
				_isRunning = false;
				BtnStartStop.Content = "启动";
				TxtStatus.Text = "状态：已停止";
				// 设置 IconStatus 为红色
				IconStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);  // 红色
				_configWriter.DeleteConfig();

				if (_activeServer != null)
				{
					_activeServer.IsActive = false;
					_activeServer = null;
				}

				// 🔧 添加这一行：触发 SelectionChanged 重新评估按钮状态
				ServersListView_SelectionChanged(ServersListView, null!);
			}
		}
	}

	private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is not ComboBox comboBox)
		{
			return;
		}

		switch (comboBox.SelectedIndex)
		{
			case 0:
				_currentProxyMode = ProxyMode.Global;
				break;
			case 1:
				_currentProxyMode = ProxyMode.PAC;
				break;
			case 2:
				_currentProxyMode = ProxyMode.Direct;
				break;
		}

		if (_isRunning)
		{
			_proxyService.SetProxyMode(_currentProxyMode);
		}
	}

	private ServerEntry? ParseSSUrl(string ssUrl)
	{
		if (string.IsNullOrWhiteSpace(ssUrl) || !ssUrl.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var content = ssUrl[5..].Trim();
		var breakIndex = content.IndexOfAny(new[] { '\r', '\n', '\t', ' ' });
		if (breakIndex >= 0)
		{
			content = content[..breakIndex];
		}

		if (content.Length == 0)
		{
			return null;
		}

		string? remark = null;
		var remarkIndex = content.IndexOf('#');
		if (remarkIndex >= 0)
		{
			var remarkPart = content[(remarkIndex + 1)..];
			if (!string.IsNullOrEmpty(remarkPart))
			{
				remark = DecodeUriComponent(remarkPart);
			}

			content = content[..remarkIndex];
		}

		var queryIndex = content.IndexOf('?');
		if (queryIndex >= 0)
		{
			content = content[..queryIndex];
		}

		content = content.Trim();
		if (content.Length == 0)
		{
			return null;
		}

		string profile;
		if (!TryDecodeBase64Profile(content, out profile))
		{
			profile = DecodeUriComponent(content);
		}

		if (!TryParseProfile(profile, out var entry))
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(remark))
		{
			entry.Name = remark;
		}
		else if (string.IsNullOrWhiteSpace(entry.Name))
		{
			entry.Name = $"{entry.Host}:{entry.Port}";
		}

		return entry;
	}

	private static bool TryDecodeBase64Profile(string payload, out string profile)
	{
		profile = string.Empty;
		if (string.IsNullOrWhiteSpace(payload))
		{
			return false;
		}

		var normalized = payload.Trim().Replace('-', '+').Replace('_', '/');
		var remainder = normalized.Length % 4;
		if (remainder == 1)
		{
			return false;
		}
		else if (remainder > 0)
		{
			normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');
		}

		try
		{
			var data = Convert.FromBase64String(normalized);
			profile = Encoding.UTF8.GetString(data);
			return profile.Contains("@") && profile.Contains(":");
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private static bool TryParseProfile(string profile, out ServerEntry entry)
	{
		entry = null!;
		if (string.IsNullOrWhiteSpace(profile))
		{
			return false;
		}

		profile = profile.Trim().TrimEnd('/');
		var atIndex = profile.LastIndexOf('@');
		if (atIndex <= 0 || atIndex == profile.Length - 1)
		{
			return false;
		}

		var methodAndPassword = profile[..atIndex];
		var hostAndPort = profile[(atIndex + 1)..];

		var colonIndex = methodAndPassword.IndexOf(':');
		if (colonIndex <= 0 || colonIndex == methodAndPassword.Length - 1)
		{
			return false;
		}

		var method = methodAndPassword[..colonIndex].Trim();
		var password = methodAndPassword[(colonIndex + 1)..];
		if (method.Length == 0 || password.Length == 0)
		{
			return false;
		}

		if (!TryParseHostAndPort(hostAndPort, out var host, out var port))
		{
			return false;
		}

		entry = new ServerEntry
		{
			Method = method,
			Password = password,
			Host = host,
			Port = port
		};

		return true;
	}

	private static bool TryParseHostAndPort(string hostAndPort, out string host, out int port)
	{
		host = string.Empty;
		port = 0;

		if (string.IsNullOrWhiteSpace(hostAndPort))
		{
			return false;
		}

		hostAndPort = hostAndPort.Trim();
		string portSegment;

		if (hostAndPort.StartsWith("["))
		{
			var closing = hostAndPort.IndexOf(']');
			if (closing <= 0 || closing == hostAndPort.Length - 1)
			{
				return false;
			}

			host = hostAndPort.Substring(1, closing - 1);
			if (closing + 1 >= hostAndPort.Length || hostAndPort[closing + 1] != ':')
			{
				return false;
			}

			portSegment = hostAndPort[(closing + 2)..];
		}
		else
		{
			var lastColon = hostAndPort.LastIndexOf(':');
			if (lastColon <= 0 || lastColon == hostAndPort.Length - 1)
			{
				return false;
			}

			host = hostAndPort[..lastColon];
			portSegment = hostAndPort[(lastColon + 1)..];
		}

		if (!int.TryParse(portSegment, out port) || port <= 0 || port > 65535)
		{
			return false;
		}

		host = host.Trim();
		return host.Length > 0;
	}

	private static string DecodeUriComponent(string value)
	{
		try
		{
			return Uri.UnescapeDataString(value.Replace('+', ' '));
		}
		catch
		{
			return value;
		}
	}

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
}

public class ServerEntry : INotifyPropertyChanged
{
	public string Name { get; set; } = string.Empty;
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; }
	public string Method { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;

	private bool _isActive;
	public bool IsActive
	{
		get => _isActive;
		set
		{
			if (_isActive != value)
			{
				_isActive = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsActiveVisibility));
			}
		}
	}

	public Visibility IsActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

	public event PropertyChangedEventHandler? PropertyChanged;

	public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public ServerEntry Clone()
	{
		return new ServerEntry
		{
			Name = Name,
			Host = Host,
			Port = Port,
			Password = Password,
			Method = Method,
			IsActive = IsActive
		};
	}
}
