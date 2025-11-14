using System;
using System.Threading;
using System.Threading.Tasks;
using App2.Models;
using App2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.UI;

namespace App2.ViewModels;

public partial class ServerDetailViewModel : ObservableObject, IDisposable
{
	private readonly ServerListViewModel _serverList;
	private readonly LatencyTestService _latencyTestService;
	private CancellationTokenSource? _latencyCancellation;
	private bool _isTesting;
	private string _selectedName = "未选择";
	private string _selectedHost = "未选择";
	private string _selectedPort = "未选择";
	private string _selectedMethod = "未选择";

	private static Color Gray() => Color.FromArgb(255, 128, 128, 128);
	private static Color Green() => Color.FromArgb(255, 0, 128, 0);
	private static Color LightGreen() => Color.FromArgb(255, 144, 238, 144);
	private static Color Orange() => Color.FromArgb(255, 255, 165, 0);
	private static Color OrangeRed() => Color.FromArgb(255, 255, 69, 0);
	private static Color Red() => Color.FromArgb(255, 255, 0, 0);

	private string _latencyText = "--";
	private Color _latencyColor = Gray();

	public ServerDetailViewModel(ServerListViewModel serverList, LatencyTestService latencyTestService)
	{
		_serverList = serverList;
		_latencyTestService = latencyTestService;

		_serverList.SelectedServerChanged += OnSelectedServerChanged;
		UpdateSelectedServer(_serverList.SelectedServer);
	}

	public string SelectedName
	{
		get => _selectedName;
		private set => SetProperty(ref _selectedName, value);
	}

	public string SelectedHost
	{
		get => _selectedHost;
		private set => SetProperty(ref _selectedHost, value);
	}

	public string SelectedPort
	{
		get => _selectedPort;
		private set => SetProperty(ref _selectedPort, value);
	}

	public string SelectedMethod
	{
		get => _selectedMethod;
		private set => SetProperty(ref _selectedMethod, value);
	}

	public string LatencyText
	{
		get => _latencyText;
		private set => SetProperty(ref _latencyText, value);
	}

	public Color LatencyColor
	{
		get => _latencyColor;
		private set => SetProperty(ref _latencyColor, value);
	}

	private void OnSelectedServerChanged(object? sender, EventArgs e)
	{
		UpdateSelectedServer(_serverList.SelectedServer);
	}

	private void UpdateSelectedServer(ServerEntry? server)
	{
		_latencyCancellation?.Cancel();
		_latencyCancellation?.Dispose();
		_latencyCancellation = null;

		if (server == null)
		{
			SelectedName = "未选择";
			SelectedHost = "未选择";
			SelectedPort = "未选择";
			SelectedMethod = "未选择";
			LatencyText = "--";
			LatencyColor = Gray();
		}
		else
		{
			SelectedName = server.Name;
			SelectedHost = server.Host;
			SelectedPort = server.Port.ToString();
			SelectedMethod = server.Method;
			LatencyText = "测试中...";
			LatencyColor = Gray();
			_ = RunLatencyTestAsync(server);
		}

		UpdateCommandState();
	}

	private async Task RunLatencyTestAsync(ServerEntry server)
	{
		_latencyCancellation?.Cancel();
		_latencyCancellation?.Dispose();
		_latencyCancellation = new CancellationTokenSource();

		_isTesting = true;
		UpdateCommandState();

		try
		{
			var result = await _latencyTestService.TestLatencyWithRetryAsync(
				server.Host,
				server.Port,
				retryCount: 2,
				timeoutMs: 3000,
				_latencyCancellation.Token);

			if (_serverList.SelectedServer == server)
			{
				UpdateLatencyDisplay(result);
			}
		}
		catch (OperationCanceledException)
		{
			// ignore
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"延迟测试失败: {ex.Message}");
			if (_serverList.SelectedServer == server)
			{
				LatencyText = "测试失败";
				LatencyColor = Red();
			}
		}
		finally
		{
			_isTesting = false;
			UpdateCommandState();
		}
	}

	private bool CanTestLatency() => _serverList.SelectedServer != null && !_isTesting;

	[RelayCommand(CanExecute = nameof(CanTestLatency))]
	private async Task TestLatencyAsync()
	{
		var server = _serverList.SelectedServer;
		if (server == null)
		{
			return;
		}

		await RunLatencyTestAsync(server);
	}

	private void UpdateLatencyDisplay(LatencyTestResult result)
	{
		LatencyText = result.StatusText;
		LatencyColor = result.Level switch
		{
			LatencyLevel.Excellent => Green(),
			LatencyLevel.Good => LightGreen(),
			LatencyLevel.Fair => Orange(),
			LatencyLevel.Poor => OrangeRed(),
			LatencyLevel.Timeout => Red(),
			_ => Gray()
		};
	}

	private void UpdateCommandState()
	{
		TestLatencyCommand.NotifyCanExecuteChanged();
	}

	public void Dispose()
	{
		_latencyCancellation?.Cancel();
		_latencyCancellation?.Dispose();
	}
}
