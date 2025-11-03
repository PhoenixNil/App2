using App2.Models;
using App2.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace App2.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	#region Fields

	private readonly ConfigWriter _configWriter;
	private readonly EngineService _engineService;
	private readonly ProxyService _proxyService;
	private readonly ConfigStorage _configStorage;
	private readonly LatencyTestService _latencyTestService;
	private readonly PACServerService _pacServerService;
	
	private DispatcherQueue? _dispatcherQueue;

	private ServerEntry? _selectedServer;
	private ServerEntry? _activeServer;
	private bool _isRunning;
	private bool _isTestingLatency;
	private ProxyMode _currentProxyMode = ProxyMode.PAC;
	private int _localPort = DefaultLocalPort;
	private ElementTheme _currentTheme = ElementTheme.Default;
	private readonly Queue<string> _logEntries = new();
	private CancellationTokenSource? _latencyTestCancellation;

	private const int DefaultLocalPort = 10808;
	private const int MinimumLocalPort = 1024;
	private const int MaximumLocalPort = 65535;
	private const int MaxLogEntries = 100;

	#endregion

	#region Properties

	public ObservableCollection<ServerEntry> Servers { get; } = new();

	private string _statusText = "状态：未启动";
	public string StatusText
	{
		get => _statusText;
		set => SetProperty(ref _statusText, value);
	}

	private Brush _statusIconForeground;
	public Brush StatusIconForeground
	{
		get => _statusIconForeground;
		set => SetProperty(ref _statusIconForeground, value);
	}

	public ServerEntry? SelectedServer
	{
		get => _selectedServer;
		set
		{
			if (SetProperty(ref _selectedServer, value))
			{
				OnSelectedServerChanged();
				// 通知测试延迟命令重新评估 CanExecute
				(TestLatencyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
			}
		}
	}

	private string _selectedName = "未选择";
	public string SelectedName
	{
		get => _selectedName;
		set => SetProperty(ref _selectedName, value);
	}

	private string _selectedHost = "未选择";
	public string SelectedHost
	{
		get => _selectedHost;
		set => SetProperty(ref _selectedHost, value);
	}

	private string _selectedPort = "未选择";
	public string SelectedPort
	{
		get => _selectedPort;
		set => SetProperty(ref _selectedPort, value);
	}

	private string _selectedMethod = "未选择";
	public string SelectedMethod
	{
		get => _selectedMethod;
		set => SetProperty(ref _selectedMethod, value);
	}

	private string _latencyText = "--";
	public string LatencyText
	{
		get => _latencyText;
		set => SetProperty(ref _latencyText, value);
	}

	private Brush _latencyForeground;
	public Brush LatencyForeground
	{
		get => _latencyForeground;
		set => SetProperty(ref _latencyForeground, value);
	}

	private string _localPortText = DefaultLocalPort.ToString();
	public string LocalPortText
	{
		get => _localPortText;
		set => SetProperty(ref _localPortText, value);
	}

	private string _startStopButtonContent = "启动";
	public string StartStopButtonContent
	{
		get => _startStopButtonContent;
		set => SetProperty(ref _startStopButtonContent, value);
	}

	private bool _startStopButtonChecked;
	public bool StartStopButtonChecked
	{
		get => _startStopButtonChecked;
		set => SetProperty(ref _startStopButtonChecked, value);
	}

	public bool IsRunning
	{
		get => _isRunning;
		private set
		{
			if (SetProperty(ref _isRunning, value))
			{
				UpdateCommandStates();
			}
		}
	}

	private bool _canEditServer;
	public bool CanEditServer
	{
		get => _canEditServer;
		set => SetProperty(ref _canEditServer, value);
	}

	private bool _canRemoveServer;
	public bool CanRemoveServer
	{
		get => _canRemoveServer;
		set => SetProperty(ref _canRemoveServer, value);
	}

	private bool _canTestLatency = true;
	public bool CanTestLatency
	{
		get => _canTestLatency;
		set
		{
			if (SetProperty(ref _canTestLatency, value))
			{
				// 通知命令重新评估 CanExecute
				(TestLatencyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
			}
		}
	}

	private int _proxyModeIndex = 1; // PAC mode by default
	public int ProxyModeIndex
	{
		get => _proxyModeIndex;
		set
		{
			if (SetProperty(ref _proxyModeIndex, value))
			{
				OnProxyModeChanged(value);
			}
		}
	}

	public ElementTheme CurrentTheme
	{
		get => _currentTheme;
		set => SetProperty(ref _currentTheme, value);
	}

	#endregion

	#region Commands

	public ICommand StartStopCommand { get; }
	public ICommand TestLatencyCommand { get; }
	public ICommand ImportSSCommand { get; }
	public ICommand AddManualCommand { get; }
	public ICommand EditServerCommand { get; }
	public ICommand RemoveServerCommand { get; }

	#endregion

	#region Constructor

	public MainWindowViewModel()
	{
		_configWriter = new ConfigWriter();
		_engineService = new EngineService();
		_proxyService = new ProxyService();
		_configStorage = new ConfigStorage();
		_latencyTestService = new LatencyTestService();
		_pacServerService = new PACServerService(7090);

		_engineService.LogReceived += OnEngineLogReceived;
		_pacServerService.LogReceived += OnPACLogReceived;

		// Initialize commands
		StartStopCommand = new AsyncRelayCommand(ExecuteStartStopAsync, CanExecuteStartStop);
		TestLatencyCommand = new AsyncRelayCommand(ExecuteTestLatencyAsync, CanExecuteTestLatency);
		ImportSSCommand = new RelayCommand(() => { }); // Will be handled by View
		AddManualCommand = new RelayCommand(() => { }); // Will be handled by View
		EditServerCommand = new RelayCommand(() => { }); // Will be handled by View
		RemoveServerCommand = new RelayCommand(() => { }); // Will be handled by View

		// Initialize brushes
		_statusIconForeground = new SolidColorBrush(Colors.Red);
		_latencyForeground = new SolidColorBrush(Colors.Gray);

		LoadServers();
		Servers.CollectionChanged += Servers_CollectionChanged;
	}

	#endregion

	#region Public Methods

	public void Initialize(DispatcherQueue dispatcherQueue)
	{
		_dispatcherQueue = dispatcherQueue;
	}

	public async Task CleanupAsync()
	{
		if (_isRunning)
		{
			_engineService.Stop();
			_proxyService.ClearProxy();
			// Wait for the PAC server to stop completely before continuing cleanup
			await _pacServerService.StopAsync().ConfigureAwait(false);
		}

		_latencyTestCancellation?.Cancel();
		_latencyTestCancellation?.Dispose();

		_engineService.Dispose();
		_pacServerService.Dispose();
		_configWriter.DeleteConfig();
	}

	public ServerEntry? ParseSSUrl(string ssUrl)
	{
		if (string.IsNullOrWhiteSpace(ssUrl) || !ssUrl.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
			return null;

		var content = ssUrl[5..].Trim();

		// strip first whitespace block
		var breakIndex = content.IndexOfAny(new[] { '\r', '\n', '\t', ' ' });
		if (breakIndex >= 0) content = content[..breakIndex];

		if (content.Length == 0) return null;

		// extract remark (#...) then query (?...)
		string? remark = null;
		var remarkIndex = content.IndexOf('#');
		if (remarkIndex >= 0)
		{
			var remarkPart = content[(remarkIndex + 1)..];
			if (!string.IsNullOrEmpty(remarkPart)) remark = DecodeUriComponent(remarkPart);
			content = content[..remarkIndex];
		}

		var queryIndex = content.IndexOf('?');
		if (queryIndex >= 0) content = content[..queryIndex];

		content = DecodeUriComponent(content);

		content = content.Trim();
		if (content.Length == 0) return null;

		// Try 3 patterns in order
		// A) whole payload is base64 of method:password@host:port
		if (TryDecodeBase64(content, out var decodedWhole) && TryParseProfile(decodedWhole, out var entryA))
			return FinalizeEntry(entryA, remark);

		// B) SIP002: only userinfo (left of '@') is base64 of method:password
		var at = content.LastIndexOf('@');
		if (at > 0 && at < content.Length - 1)
		{
			var left = content[..at];     // userinfo (maybe base64)
			var right = content[(at + 1)..]; // host:port

			if (TryDecodeBase64(left, out var userinfoDecoded) && userinfoDecoded.Contains(':'))
			{
				var profile = $"{userinfoDecoded}@{DecodeUriComponent(right)}";
				if (TryParseProfile(profile, out var entryB))
					return FinalizeEntry(entryB, remark);
			}
		}

		// C) Plain text: method:password@host:port
		var plain = DecodeUriComponent(content);
		if (TryParseProfile(plain, out var entryC))
			return FinalizeEntry(entryC, remark);

		return null;

		static ServerEntry FinalizeEntry(ServerEntry entry, string? remarkText)
		{
			if (!string.IsNullOrWhiteSpace(remarkText))
				entry.Name = remarkText;
			else if (string.IsNullOrWhiteSpace(entry.Name))
				entry.Name = $"{entry.Host}:{entry.Port}";
			return entry;
		}
	}

	public ServerEntry CreateServerEntry(string name, string host, string port, string password, string method)
	{
		if (!int.TryParse(port, out var portValue) || portValue <= 0 || portValue > 65535)
		{
			throw new ArgumentException("Invalid port number");
		}

		if (string.IsNullOrWhiteSpace(host))
		{
			throw new ArgumentException("Host cannot be empty");
		}

		if (string.IsNullOrWhiteSpace(method))
		{
			method = "aes-256-gcm";
		}

		return new ServerEntry
		{
			Name = string.IsNullOrWhiteSpace(name) ? "未命名节点" : name.Trim(),
			Host = host.Trim(),
			Port = portValue,
			Password = password.Trim(),
			Method = method.Trim()
		};
	}

	public void AddServer(ServerEntry server)
	{
		Servers.Add(server);
		SelectedServer = server;
	}

	public void UpdateServer(ServerEntry target, ServerEntry source)
	{
		target.Name = source.Name;
		target.Host = source.Host;
		target.Port = source.Port;
		target.Password = source.Password;
		target.Method = source.Method;

		target.OnPropertyChanged(nameof(ServerEntry.Name));
		target.OnPropertyChanged(nameof(ServerEntry.Host));
		target.OnPropertyChanged(nameof(ServerEntry.Port));
		target.OnPropertyChanged(nameof(ServerEntry.Method));

		UpdateDetails(target);
	}

	public bool RemoveServer(ServerEntry server)
	{
		var index = Servers.IndexOf(server);
		if (index < 0) return false;

		Servers.Remove(server);

		if (_isRunning && _activeServer == server)
		{
			_ = StopAsync();
		}

		// Select next server
		if (Servers.Count > 0)
		{
			var nextIndex = Math.Min(index, Servers.Count - 1);
			SelectedServer = Servers[nextIndex];
		}
		else
		{
			SelectedServer = null;
		}

		return true;
	}

	public bool ValidateAndUpdateLocalPort(double value, out string? errorMessage)
	{
		errorMessage = null;

		if (double.IsNaN(value))
		{
			errorMessage = "请输入有效的端口号。";
			return false;
		}

		var newPort = (int)Math.Round(value);
		if (newPort < MinimumLocalPort || newPort > MaximumLocalPort)
		{
			errorMessage = $"端口号必须在 {MinimumLocalPort} 至 {MaximumLocalPort} 之间。";
			return false;
		}

		if (newPort != _localPort)
		{
			_localPort = newPort;
			LocalPortText = _localPort.ToString();
			return true; // Port changed
		}

		return false; // Port not changed
	}

	public string GetLogsText()
	{
		return _logEntries.Count > 0
			? string.Join(Environment.NewLine, _logEntries)
			: "暂无日志记录。";
	}

	public bool HasLogs => _logEntries.Count > 0;

	public void ApplyTheme(ElementTheme theme)
	{
		CurrentTheme = theme;
	}

	#endregion

	#region Private Methods

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

	private void OnSelectedServerChanged()
	{
		var hasSelection = _selectedServer != null;
		var canModify = hasSelection && !_selectedServer!.IsActive;

		CanEditServer = canModify;
		CanRemoveServer = canModify;

		if (!hasSelection)
		{
			if (_isRunning && (_activeServer == null || !Servers.Contains(_activeServer)))
			{
				_ = StopAsync();
			}

			UpdateDetails(null);
			return;
		}

		UpdateDetails(_selectedServer);

		// Auto test latency
		if (_selectedServer != null)
		{
			_ = TestServerLatencyAsync(_selectedServer);
		}
	}

	private void UpdateDetails(ServerEntry? server)
	{
		if (server == null)
		{
			SelectedName = "未选择";
			SelectedHost = "未选择";
			SelectedPort = "未选择";
			SelectedMethod = "未选择";
			LatencyText = "--";
			LatencyForeground = new SolidColorBrush(Colors.Gray);
			return;
		}

		SelectedName = server.Name;
		SelectedHost = server.Host;
		SelectedPort = server.Port.ToString();
		SelectedMethod = server.Method;
		LatencyText = "测试中...";
		LatencyForeground = new SolidColorBrush(Colors.Gray);
	}

	private async Task TestServerLatencyAsync(ServerEntry server)
	{
		// Cancel previous test
		_latencyTestCancellation?.Cancel();
		_latencyTestCancellation?.Dispose();
		_latencyTestCancellation = new CancellationTokenSource();

		_isTestingLatency = true;
		CanTestLatency = false;

		try
		{
			var result = await _latencyTestService.TestLatencyWithRetryAsync(
				server.Host,
				server.Port,
				retryCount: 2,
				timeoutMs: 3000,
				_latencyTestCancellation.Token);

			// Ensure this server is still selected
			if (_selectedServer == server)
			{
				UpdateLatencyDisplay(result);
			}
		}
		catch (OperationCanceledException)
		{
			// Test cancelled, don't update UI
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"延迟测试失败: {ex.Message}");
			if (_selectedServer == server)
			{
				LatencyText = "测试失败";
				LatencyForeground = new SolidColorBrush(Colors.Red);
			}
		}
		finally
		{
			_isTestingLatency = false;
			CanTestLatency = true;
		}
	}

	private void UpdateLatencyDisplay(LatencyTestResult result)
	{
		LatencyText = result.StatusText;

		// Set color based on latency level
		LatencyForeground = result.Level switch
		{
			LatencyLevel.Excellent => new SolidColorBrush(Colors.Green),
			LatencyLevel.Good => new SolidColorBrush(Colors.LightGreen),
			LatencyLevel.Fair => new SolidColorBrush(Colors.Orange),
			LatencyLevel.Poor => new SolidColorBrush(Colors.OrangeRed),
			LatencyLevel.Timeout => new SolidColorBrush(Colors.Red),
			_ => new SolidColorBrush(Colors.Gray)
		};
	}

	private void OnEngineLogReceived(object? sender, string log)
	{
		// Ensure UI updates happen on the UI thread
		_dispatcherQueue?.TryEnqueue(() =>
		{
			var timestamped = $"[{DateTime.Now:HH:mm:ss}] {log}";
			Debug.WriteLine(timestamped);

			_logEntries.Enqueue(timestamped);
			while (_logEntries.Count > MaxLogEntries)
			{
				_logEntries.Dequeue();
			}

			if (log.Contains("listening on", StringComparison.OrdinalIgnoreCase))
			{
				StatusText = "状态：运行中";
				StatusIconForeground = new SolidColorBrush(Colors.Green);
			}
		});
	}

	private void OnPACLogReceived(object? sender, string log)
	{
		// Ensure UI updates happen on the UI thread
		_dispatcherQueue?.TryEnqueue(() =>
		{
			var timestamped = $"[{DateTime.Now:HH:mm:ss}] [PAC] {log}";
			Debug.WriteLine(timestamped);

			_logEntries.Enqueue(timestamped);
			while (_logEntries.Count > MaxLogEntries)
			{
				_logEntries.Dequeue();
			}
		});
	}

	private async void OnProxyModeChanged(int index)
	{
		_currentProxyMode = index switch
		{
			0 => ProxyMode.Global,
			1 => ProxyMode.PAC,
			2 => ProxyMode.Direct,
			_ => ProxyMode.PAC
		};

		if (_isRunning)
		{
			// Stop PAC server if switching away from PAC mode
			if (_currentProxyMode != ProxyMode.PAC)
			{
				await _pacServerService.StopAsync();
			}
			// Start PAC server if switching to PAC mode
			else if (_currentProxyMode == ProxyMode.PAC)
			{
				try
				{
					await _pacServerService.StartAsync($"127.0.0.1:{_localPort}");
					_proxyService.SetPACUrl(_pacServerService.PACUrl);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"启动 PAC 服务器失败: {ex.Message}");
				}
			}
			
			_proxyService.SetProxyMode(_currentProxyMode);
		}
	}

	private void UpdateCommandStates()
	{
		OnPropertyChanged(nameof(IsRunning));
	}

	#endregion

	#region Command Implementations

	private bool CanExecuteStartStop() => true;

	private async Task ExecuteStartStopAsync()
	{
		if (!_isRunning)
		{
			if (_selectedServer == null)
			{
				// This should be handled by the View
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
		if (_selectedServer == null)
		{
			return;
		}

		try
		{
			_configWriter.WriteConfig(_selectedServer, _localPort);
			var configPath = _configWriter.GetConfigPath();
			if (!File.Exists(configPath))
			{
				throw new InvalidOperationException($"配置文件生成失败: {configPath}");
			}

			_engineService.Start(configPath);
			
			// Start PAC server if in PAC mode
			if (_currentProxyMode == ProxyMode.PAC)
			{
				await _pacServerService.StartAsync($"127.0.0.1:{_localPort}");
				_proxyService.SetPACUrl(_pacServerService.PACUrl);
			}
			
			_proxyService.SetProxyServer("127.0.0.1", _localPort);
			_proxyService.SetProxyMode(_currentProxyMode);

			IsRunning = true;
			StartStopButtonContent = "停止";
			StartStopButtonChecked = true;
			StatusText = "状态：运行中";
			StatusIconForeground = new SolidColorBrush(Colors.Green);

			if (_activeServer != null)
			{
				_activeServer.IsActive = false;
			}

			_selectedServer.IsActive = true;
			_activeServer = _selectedServer;

			// Update command states
			OnSelectedServerChanged();
		}
		catch (Exception ex)
		{
			IsRunning = false;
			StartStopButtonChecked = false;
			StatusText = $"启动失败: {ex.Message}";

			throw; // Re-throw to let View handle the error dialog
		}
	}

	private async Task StopAsync()
	{
		try
		{
			_engineService.Stop();
			_proxyService.ClearProxy();
			await _pacServerService.StopAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"停止引擎失败: {ex.Message}");
		}
		finally
		{
			IsRunning = false;
			StartStopButtonContent = "启动";
			StartStopButtonChecked = false;
			StatusText = "状态：已停止";
			StatusIconForeground = new SolidColorBrush(Colors.Red);
			_configWriter.DeleteConfig();

			if (_activeServer != null)
			{
				_activeServer.IsActive = false;
				_activeServer = null;
			}

			// Update command states
			OnSelectedServerChanged();
		}
	}

	private bool CanExecuteTestLatency() => !_isTestingLatency && _selectedServer != null;

	private async Task ExecuteTestLatencyAsync()
	{
		if (_selectedServer == null || _isTestingLatency)
		{
			return;
		}

		await TestServerLatencyAsync(_selectedServer);
	}

	#endregion

	#region Helper Methods

	private static bool TryDecodeBase64(string payload, out string result)
	{
		result = string.Empty;
		if (string.IsNullOrWhiteSpace(payload)) return false;

		var normalized = payload.Trim().Replace('-', '+').Replace('_', '/');
		var remainder = normalized.Length % 4;
		if (remainder == 1) return false;
		if (remainder > 0) normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');

		try
		{
			var data = Convert.FromBase64String(normalized);
			result = Encoding.UTF8.GetString(data);
			return true;
		}
		catch
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

	#endregion
}
