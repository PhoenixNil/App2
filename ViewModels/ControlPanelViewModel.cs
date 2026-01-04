using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using App2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace App2.ViewModels;

public partial class ControlPanelViewModel : ObservableObject
{
	private const int DefaultLocalPort = 10808;
	private const int MaxLogEntries = 100;
	private const int MinimumLocalPort = 1024;
	private const int MaximumLocalPort = 65535;

	private readonly ConfigWriter _configWriter;
	private readonly EngineService _engineService;
	private readonly ProxyService _proxyService;
	private readonly PACServerService _pacServerService;
	private readonly IDialogService _dialogService;
	private readonly IThemeService _themeService;
	private readonly AutoStartService _autoStartService;
	private readonly TunService _tunService;
	private readonly ServerListViewModel _serverList;
	private readonly Queue<string> _logEntries = new();
	private bool _pendingServerListCheck;

	private DispatcherQueue? _dispatcherQueue;
	private ProxyMode _currentProxyMode = ProxyMode.PAC;
	private int _localPort = DefaultLocalPort;
	private bool _isRunning;
	private string _statusText = "状态：未运行";
	private Color _statusIconColor = Color.FromArgb(255, 255, 0, 0);
	private string _startStopText = "启动";
	private bool _startStopChecked;
	private string _localPortText = DefaultLocalPort.ToString();
	private bool _isBypassChinaMode = true;
	private int _proxyModeIndex = 1;
	private Color _routeBadgeForeground = Color.FromArgb(255, 30, 144, 255);
	private Color _routeBadgeBackground = Color.FromArgb(32, 30, 144, 255);
	private bool _isThemePickerOpen;
	private bool _isAutoStartEnabled;
	private bool _isAutoStartStateInternalUpdate;
	private bool _isTunEnabled;
	private string? _currentTunServerHost; // TUN 模式运行时的服务器地址（用于清理路由）

	public ControlPanelViewModel(
		ConfigWriter configWriter,
		EngineService engineService,
		ProxyService proxyService,
		PACServerService pacServerService,
		IDialogService dialogService,
		IThemeService themeService,
		AutoStartService autoStartService,
		TunService tunService,
		ServerListViewModel serverList)
	{
		_configWriter = configWriter;
		_engineService = engineService;
		_proxyService = proxyService;
		_pacServerService = pacServerService;
		_dialogService = dialogService;
		_themeService = themeService;
		_autoStartService = autoStartService;
		_tunService = tunService;
		_serverList = serverList;

		_engineService.LogReceived += OnEngineLogReceived;
		_pacServerService.LogReceived += OnPacLogReceived;
		_serverList.Servers.CollectionChanged += OnServersCollectionChanged;
		_themeService.ThemeChanged += ThemeServiceOnThemeChanged;

		UpdateRouteModeColors();
		RaiseThemeStateChanged();
		InitializeAutoStartState();
	}

	public ElementTheme CurrentTheme => _themeService.CurrentTheme;

	public bool TrueValue => true;
	public bool FalseValue => false;
	public ElementTheme LightTheme => ElementTheme.Light;
	public ElementTheme DarkTheme => ElementTheme.Dark;
	public ElementTheme DefaultTheme => ElementTheme.Default;
	public bool IsLightThemeEnabled => _themeService.ActualTheme != ElementTheme.Light;
	public bool IsDarkThemeEnabled => _themeService.ActualTheme != ElementTheme.Dark;
	public bool IsDefaultThemeEnabled => true;

	public bool IsThemePickerOpen
	{
		get => _isThemePickerOpen;
		set => SetProperty(ref _isThemePickerOpen, value);
	}

	public bool IsRunning
	{
		get => _isRunning;
		private set
		{
			if (SetProperty(ref _isRunning, value))
			{
				_serverList.IsRunning = value;
				EditLocalPortCommand.NotifyCanExecuteChanged();
				OnPropertyChanged(nameof(CanEditRouteSettings));
				OnPropertyChanged(nameof(CanChangeProxyMode));
				OnPropertyChanged(nameof(CanChangeTunMode));
			}
		}
	}

	public string StatusText
	{
		get => _statusText;
		private set => SetProperty(ref _statusText, value);
	}

	public Color StatusIconColor
	{
		get => _statusIconColor;
		private set => SetProperty(ref _statusIconColor, value);
	}

	public string StartStopButtonContent
	{
		get => _startStopText;
		private set => SetProperty(ref _startStopText, value);
	}

	public bool StartStopButtonChecked
	{
		get => _startStopChecked;
		private set => SetProperty(ref _startStopChecked, value);
	}

	public string LocalPortText
	{
		get => _localPortText;
		private set => SetProperty(ref _localPortText, value);
	}

	public bool CanEditRouteSettings => !IsRunning;
	public bool CanChangeProxyMode => !IsRunning;
	public bool CanChangeTunMode => !IsRunning;

	public bool IsTunEnabled
	{
		get => _isTunEnabled;
		set => SetProperty(ref _isTunEnabled, value);
	}

	public bool IsAutoStartEnabled
	{
		get => _isAutoStartEnabled;
		set
		{
			if (SetProperty(ref _isAutoStartEnabled, value))
			{
				if (_isAutoStartStateInternalUpdate)
				{
					return;
				}

				_ = ApplyAutoStartPreferenceAsync(value);
			}
		}
	}

	public bool IsBypassChinaMode
	{
		get => _isBypassChinaMode;
		set
		{
			if (SetProperty(ref _isBypassChinaMode, value))
			{
				OnPropertyChanged(nameof(IsGlobalRouteMode));
				OnPropertyChanged(nameof(RouteModeBadgeText));
				OnPropertyChanged(nameof(RouteModeBadgeIcon));
				UpdateRouteModeColors();
			}
		}
	}

	public bool IsGlobalRouteMode => !_isBypassChinaMode;

	public string RouteModeBadgeText => _isBypassChinaMode ? "绕过大陆" : "全局路由";
	public string RouteModeBadgeIcon => _isBypassChinaMode ? "\uE72E" : "\uE774";
	public bool IsRouteModeBadgeVisible => _currentProxyMode == ProxyMode.Global;

	public Color RouteModeBadgeForegroundColor
	{
		get => _routeBadgeForeground;
		private set => SetProperty(ref _routeBadgeForeground, value);
	}

	public Color RouteModeBadgeBackgroundColor
	{
		get => _routeBadgeBackground;
		private set => SetProperty(ref _routeBadgeBackground, value);
	}

	public int ProxyModeIndex
	{
		get => _proxyModeIndex;
		set
		{
			if (SetProperty(ref _proxyModeIndex, value))
			{
				_ = ApplyProxyModeAsync();
			}
		}
	}

	public void Initialize(DispatcherQueue dispatcherQueue)
	{
		_dispatcherQueue = dispatcherQueue;
	}

	public async Task CleanupAsync()
	{
		try
		{
			if (IsRunning)
			{
				_engineService.Stop();
				_proxyService.ClearProxy();
			}
		}
		finally
		{
			await _pacServerService.StopAsync().ConfigureAwait(false);
			_engineService.Dispose();
			_pacServerService.Dispose();
			_configWriter.DeleteConfig();
			_themeService.ThemeChanged -= ThemeServiceOnThemeChanged;
		}
	}

	[RelayCommand]
	private async Task StartStopAsync()
	{
		if (!IsRunning)
		{
			if (_serverList.SelectedServer == null)
			{
				await _dialogService.ShowMessageAsync("未选择服务器", "请先选择一个服务器节点");
				return;
			}

			await StartAsync();
		}
		else
		{
			await StopAsync();
		}
	}

	private async Task StartAsync()
	{
		var server = _serverList.SelectedServer;
		if (server == null)
		{
			return;
		}

		try
		{
			bool isTunMode = _isTunEnabled;
			string? outboundInterface = null;
			string? tunInterfaceName = null;
			string? tunInterfaceAddress = null;

			// TUN 模式前置检查
			if (isTunMode)
			{
				if (!_tunService.IsWintunAvailable())
				{
					var expectedPath = _tunService.GetExpectedWintunPath();
					throw new InvalidOperationException($"TUN 模式需要 wintun.dll，请将其放置到: {expectedPath}");
				}

				outboundInterface = _tunService.DetectOutboundInterface();
				if (string.IsNullOrEmpty(outboundInterface))
				{
					outboundInterface = _tunService.DetectOutboundInterface(forceRefresh: true);
				}
				if (string.IsNullOrEmpty(outboundInterface))
				{
					throw new InvalidOperationException("无法检测到有效的出站网络接口，请检查网络连接");
				}
				tunInterfaceName = _tunService.DefaultTunInterfaceName;
				tunInterfaceAddress = _tunService.DefaultTunInterfaceAddress;
			}

			string? aclPath = null;
			if (_currentProxyMode == ProxyMode.Global && _isBypassChinaMode)
			{
				var exeDir = AppContext.BaseDirectory;
				var aclFile = Path.Combine(exeDir, "shadowsocks.acl");
				if (File.Exists(aclFile))
				{
					aclPath = aclFile;
				}
			}

			// 写入配置（TUN 模式包含 DNS 配置）
			_configWriter.WriteConfig(server, _localPort, aclPath, isTunMode, isTunMode ? _tunService.DnsServers : null);
			var configPath = _configWriter.GetConfigPath();
			if (!File.Exists(configPath))
			{
				throw new InvalidOperationException($"配置文件创建失败: {configPath}");
			}

			// 启动引擎（TUN 模式带额外参数）
			_engineService.Start(
				configPath,
				isTunMode,
				outboundInterface,
				tunInterfaceName,
				tunInterfaceAddress,
				requireAdmin: isTunMode);

			// TUN 模式不需要设置系统代理，流量直接通过虚拟网卡
			if (!isTunMode)
			{
				if (_currentProxyMode == ProxyMode.PAC)
				{
					await _pacServerService.StartAsync($"127.0.0.1:{_localPort}");
					_proxyService.SetPACUrl(_pacServerService.PACUrl);
				}
				else
				{
					await _pacServerService.StopAsync();
				}

				_proxyService.SetProxyServer("127.0.0.1", _localPort);
				_proxyService.SetProxyMode(_currentProxyMode);
			}
			else
			{
				// TUN 模式确保不设置系统代理
				await _pacServerService.StopAsync();
				_proxyService.ClearProxy();
			}

			_serverList.SetActiveServer(server);

			// TUN 模式设置路由（将所有流量引导到 TUN 接口）
			if (isTunMode)
			{
				_currentTunServerHost = server.Host;
				// 等待一小段时间让 TUN 接口创建完成
				await Task.Delay(1000);
				if (!_tunService.SetupTunRoutes(server.Host))
				{
					throw new InvalidOperationException("TUN 路由设置失败，请允许管理员权限。");
				}
			}

			IsRunning = true;
			StartStopButtonContent = "停止";
			StartStopButtonChecked = true;
			StatusText = isTunMode ? "状态：TUN 运行中" : "状态：已运行";
			StatusIconColor = Color.FromArgb(255, 0, 128, 0);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"启动失败: {ex.Message}");
			try
			{
				if (_engineService.IsRunning)
				{
					_engineService.Stop();
				}
			}
			catch (Exception stopEx)
			{
				Debug.WriteLine($"启动失败后停止引擎失败: {stopEx.Message}");
			}

			await _pacServerService.StopAsync();
			_proxyService.ClearProxy();
			StatusText = $"启动失败: {ex.Message}";
			StatusIconColor = Color.FromArgb(255, 255, 0, 0);
			StartStopButtonContent = "启动";
			StartStopButtonChecked = false;
			IsRunning = false;
			await _dialogService.ShowErrorAsync("启动失败", ex.Message);
		}
	}

	private async Task StopAsync()
	{
		try
		{
			// TUN 模式清理路由
			if (_currentTunServerHost != null)
			{
				_tunService.CleanupTunRoutes(_currentTunServerHost);
				_currentTunServerHost = null;
			}

			_engineService.Stop();
			_proxyService.ClearProxy();
			await _pacServerService.StopAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"停止失败: {ex.Message}");
		}
		finally
		{
			_configWriter.DeleteConfig();
			_serverList.SetActiveServer(null);
			IsRunning = false;
			StartStopButtonContent = "启动";
			StartStopButtonChecked = false;
			StatusText = "状态：未运行";
			StatusIconColor = Color.FromArgb(255, 255, 0, 0);
		}
	}

	private bool CanEditLocalPort() => true;

	[RelayCommand(CanExecute = nameof(CanEditLocalPort))]
	private async Task EditLocalPortAsync()
	{
		var newPort = await _dialogService.ShowLocalPortDialogAsync(_localPort, MinimumLocalPort, MaximumLocalPort);
		if (!newPort.HasValue || newPort.Value == _localPort)
		{
			return;
		}

		_localPort = newPort.Value;
		LocalPortText = _localPort.ToString();

		if (IsRunning)
		{
			await _dialogService.ShowPortChangedReminderAsync();
		}
	}

	[RelayCommand]
	private async Task ViewLogsAsync()
	{
		var text = _logEntries.Count > 0
			? string.Join(Environment.NewLine, _logEntries)
			: "暂无日志记录";
		await _dialogService.ShowLogsAsync(text, _logEntries.Count > 0);
	}

	private void OnServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (!IsRunning || _serverList.ActiveServer == null || _pendingServerListCheck)
		{
			return;
		}

		_pendingServerListCheck = true;
		_dispatcherQueue?.TryEnqueue(() =>
		{
			_pendingServerListCheck = false;
			if (IsRunning && _serverList.ActiveServer != null && !_serverList.Servers.Contains(_serverList.ActiveServer))
			{
				_ = StopAsync();
			}
		});
	}

	private void OnEngineLogReceived(object? sender, string log)
	{
		EnqueueLog("ENGINE", log);
	}

	private void OnPacLogReceived(object? sender, string log)
	{
		EnqueueLog("PAC", log);
	}

	private void EnqueueLog(string category, string message)
	{
		void AddLog()
		{
			var entry = $"[{DateTime.Now:HH:mm:ss}] [{category}] {message}";
			_logEntries.Enqueue(entry);
			while (_logEntries.Count > MaxLogEntries)
			{
				_logEntries.Dequeue();
			}
		}

		if (_dispatcherQueue?.HasThreadAccess == true)
		{
			AddLog();
		}
		else
		{
			_dispatcherQueue?.TryEnqueue(AddLog);
		}
	}

	private async Task ApplyProxyModeAsync()
	{
		_currentProxyMode = ProxyModeIndex switch
		{
			0 => ProxyMode.Global,
			1 => ProxyMode.PAC,
			2 => ProxyMode.Direct,
			_ => ProxyMode.PAC
		};

		OnPropertyChanged(nameof(IsRouteModeBadgeVisible));

		if (IsRunning)
		{
			try
			{
				if (_currentProxyMode != ProxyMode.PAC)
				{
					await _pacServerService.StopAsync();
				}
				else
				{
					await _pacServerService.StartAsync($"127.0.0.1:{_localPort}");
					_proxyService.SetPACUrl(_pacServerService.PACUrl);
				}

				_proxyService.SetProxyMode(_currentProxyMode);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"切换代理模式失败: {ex.Message}");
			}
		}
	}

	private void UpdateRouteModeColors()
	{
		if (_isBypassChinaMode)
		{

		RouteModeBadgeForegroundColor = Color.FromArgb(255, 30, 144, 255);
		RouteModeBadgeBackgroundColor = Color.FromArgb(32, 30, 144, 255);
		}
		else
		{
		RouteModeBadgeForegroundColor = Color.FromArgb(255, 255, 140, 0);
		RouteModeBadgeBackgroundColor = Color.FromArgb(32, 255, 140, 0);
		}
	}

	private void RaiseThemeStateChanged()
	{
		OnPropertyChanged(nameof(CurrentTheme));
		OnPropertyChanged(nameof(IsLightThemeEnabled));
		OnPropertyChanged(nameof(IsDarkThemeEnabled));
		OnPropertyChanged(nameof(IsDefaultThemeEnabled));
	}

	private void ThemeServiceOnThemeChanged(object? sender, EventArgs e)
	{
		RaiseThemeStateChanged();
	}

	private void InitializeAutoStartState()
	{
		try
		{
			_isAutoStartStateInternalUpdate = true;
			_isAutoStartEnabled = _autoStartService.IsEnabled();
			OnPropertyChanged(nameof(IsAutoStartEnabled));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"读取开机启动状态失败: {ex.Message}");
		}
		finally
		{
			_isAutoStartStateInternalUpdate = false;
		}
	}

	private async Task ApplyAutoStartPreferenceAsync(bool enable)
	{
		try
		{
			if (enable)
			{
				_autoStartService.Enable();
			}
			else
			{
				_autoStartService.Disable();
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"更新开机启动状态失败: {ex.Message}");
			_isAutoStartStateInternalUpdate = true;
			_isAutoStartEnabled = !enable;
			OnPropertyChanged(nameof(IsAutoStartEnabled));
			_isAutoStartStateInternalUpdate = false;
			await _dialogService.ShowErrorAsync("更新开机启动失败", ex.Message);
		}
	}

	[RelayCommand]
	private void ToggleThemePicker()
	{
		IsThemePickerOpen = !IsThemePickerOpen;
	}

	[RelayCommand]
	private void ChangeTheme(ElementTheme? theme)
	{
		if (!theme.HasValue)
		{
			return;
		}

		_themeService.ApplyTheme(theme.Value);
		RaiseThemeStateChanged();
		IsThemePickerOpen = false;
	}

	[RelayCommand]
	private void ChangeRouteMode(bool isBypass)
	{
		IsBypassChinaMode = isBypass;
	}
}
