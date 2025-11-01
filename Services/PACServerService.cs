using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace App2.Services;

/// <summary>
/// 本地 HTTP 服务器，用于托管 PAC 文件
/// </summary>
public class PACServerService : IDisposable
{
	private HttpListener? _httpListener;
	private CancellationTokenSource? _cancellationTokenSource;
	private Task? _listenerTask;
	private string _pacContent = string.Empty;
	private readonly int _port;

	public event EventHandler<string>? LogReceived;

	public PACServerService(int port = 7090)
	{
		_port = port;
	}

	private void OnLog(string message)
	{
		System.Diagnostics.Debug.WriteLine($"[PAC] {message}");
		LogReceived?.Invoke(this, message);
	}

	/// <summary>
	/// 获取 PAC 文件的 URL
	/// </summary>
	public string PACUrl => $"http://127.0.0.1:{_port}/pac.js";

	/// <summary>
	/// 启动 HTTP 服务器
	/// </summary>
	public async Task StartAsync(string proxyAddress)
	{
		if (_httpListener != null && _httpListener.IsListening)
		{
			OnLog($"服务器已在运行: {PACUrl}");
			return;
		}

		OnLog($"正在启动 PAC 服务器，端口: {_port}");

		// 生成 PAC 文件内容
		_pacContent = await GeneratePACContentAsync(proxyAddress);
		OnLog($"PAC 内容已生成，长度: {_pacContent.Length} 字符");

		// 创建并启动 HTTP 监听器
		_httpListener = new HttpListener();
		_httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/");

		try
		{
			_httpListener.Start();
			OnLog($"PAC 服务器已启动: {PACUrl}");
		}
		catch (HttpListenerException ex)
		{
			OnLog($"启动失败: {ex.Message}");
			throw new InvalidOperationException($"无法启动 PAC 服务器在端口 {_port}。可能端口已被占用。", ex);
		}

		_cancellationTokenSource = new CancellationTokenSource();
		_listenerTask = Task.Run(() => HandleRequestsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
	}

	/// <summary>
	/// 停止 HTTP 服务器
	/// </summary>
	public async Task StopAsync()
	{
		if (_httpListener == null || !_httpListener.IsListening)
		{
			return;
		}

		OnLog("正在停止 PAC 服务器");

		if (_cancellationTokenSource != null)
		{
			_cancellationTokenSource.Cancel();
		}

		if (_httpListener != null && _httpListener.IsListening)
		{
			_httpListener.Stop();
		}

		if (_listenerTask != null)
		{
			try
			{
				await _listenerTask;
			}
			catch (OperationCanceledException)
			{
				// Expected when cancelling
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[PAC] 停止时出现异常: {ex.Message}");
			}
		}

		_httpListener?.Close();
		_cancellationTokenSource?.Dispose();
		OnLog("PAC 服务器已停止");
	}

	/// <summary>
	/// 生成 PAC 文件内容
	/// </summary>
	private async Task<string> GeneratePACContentAsync(string proxyAddress)
	{
		try
		{
			// 读取 pac.txt 文件
			var pacTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pac.txt");
			if (!File.Exists(pacTemplatePath))
			{
				throw new FileNotFoundException($"PAC 模板文件不存在: {pacTemplatePath}");
			}

			var content = await File.ReadAllTextAsync(pacTemplatePath, Encoding.UTF8);

			// 替换代理地址占位符
			// PAC 文件使用 SOCKS5 代理格式: SOCKS5 host:port 或 SOCKS host:port
			content = content.Replace("'__PROXY__'", $"'SOCKS5 {proxyAddress}; SOCKS {proxyAddress}'");

			return content;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"无法生成 PAC 文件内容: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// 处理 HTTP 请求
	/// </summary>
	private async Task HandleRequestsAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
		{
			try
			{
				var context = await _httpListener.GetContextAsync();
				_ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
			}
			catch (HttpListenerException)
			{
				// Listener was stopped
				break;
			}
			catch (ObjectDisposedException)
			{
				// Listener was disposed
				break;
			}
			catch (Exception)
			{
				// Log error but continue
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}
			}
		}
	}

	/// <summary>
	/// 处理单个 HTTP 请求
	/// </summary>
	private async Task ProcessRequestAsync(HttpListenerContext context)
	{
		try
		{
			var request = context.Request;
			var response = context.Response;

			// 只处理 pac.js 请求
			if (request.Url?.AbsolutePath == "/pac.js" || request.Url?.AbsolutePath == "/")
			{
				var buffer = Encoding.UTF8.GetBytes(_pacContent);
				response.ContentType = "application/x-ns-proxy-autoconfig";
				response.ContentLength64 = buffer.Length;
				response.ContentEncoding = Encoding.UTF8;
				response.StatusCode = 200;

				await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
			}
			else
			{
				response.StatusCode = 404;
			}

			response.Close();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[PAC] 处理请求时出错: {ex.Message}");
		}
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
		_httpListener?.Close();
		_cancellationTokenSource?.Dispose();
	}
}

