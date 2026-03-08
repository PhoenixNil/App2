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
	private byte[] _pacContentBytes = Array.Empty<byte>();
	private int _pacVersion = 0;
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
	public string PACUrl => $"http://127.0.0.1:{_port}/pac.js?v={_pacVersion}";

	/// <summary>
	/// 启动 HTTP 服务器
	/// </summary>
	/// <param name="proxyAddress">本地代理地址（host:port）</param>
	/// <param name="forceGlobalSocks">是否生成“全部走 SOCKS5”的 PAC</param>
	public async Task StartAsync(string proxyAddress, bool forceGlobalSocks = false)
	{
		var newPacContent = await GeneratePACContentAsync(proxyAddress, forceGlobalSocks);
		var pacChanged = !string.Equals(_pacContent, newPacContent, StringComparison.Ordinal);
		_pacContent = newPacContent;
		if (pacChanged || _pacContentBytes.Length == 0)
		{
			_pacContentBytes = Encoding.UTF8.GetBytes(_pacContent);
		}
		if (pacChanged)
		{
			_pacVersion++;
			OnLog($"PAC 内容已更新，版本: {_pacVersion}");
		}

		if (_httpListener != null && _httpListener.IsListening)
		{
			OnLog($"服务器已在运行，PAC 已热更新: {PACUrl}");
			return;
		}

		OnLog($"正在启动 PAC 服务器，端口: {_port}");
		OnLog($"PAC 内容长度: {_pacContent.Length} 字符");

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
				await _listenerTask.ConfigureAwait(false);
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
		_httpListener = null;
		_listenerTask = null;
		_cancellationTokenSource = null;
		OnLog("PAC 服务器已停止");
	}

	/// <summary>
	/// 生成 PAC 文件内容
	/// </summary>
	private async Task<string> GeneratePACContentAsync(string proxyAddress, bool forceGlobalSocks)
	{
		try
		{
			if (forceGlobalSocks)
			{
				OnLog("使用全局 SOCKS5 PAC 模式");
				return
$@"function FindProxyForURL(url, host) {{
    return 'SOCKS5 {proxyAddress}';
}}";
			}

			// 读取 pac.txt 文件
			var pacTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pac.txt");
			if (!File.Exists(pacTemplatePath))
			{
				throw new FileNotFoundException($"PAC 模板文件不存在: {pacTemplatePath}");
			}

			var content = await File.ReadAllTextAsync(pacTemplatePath, Encoding.UTF8);

			// 替换代理地址占位符
			// 仅使用 SOCKS5，避免回退 SOCKS(v4) 导致本地 DNS 解析污染。
			content = content.Replace("'__PROXY__'", $"'SOCKS5 {proxyAddress}'");

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
				await ProcessRequestAsync(context);
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
		var response = context.Response;
		try
		{
			var request = context.Request;

			// 只处理 pac.js 请求
			if (request.Url?.AbsolutePath == "/pac.js" || request.Url?.AbsolutePath == "/")
			{
				var buffer = _pacContentBytes;
				response.ContentType = "application/x-ns-proxy-autoconfig";
				response.ContentLength64 = buffer.Length;
				response.ContentEncoding = Encoding.UTF8;
				response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
				response.Headers["Pragma"] = "no-cache";
				response.Headers["Expires"] = "0";
				response.StatusCode = 200;

				await response.OutputStream.WriteAsync(buffer.AsMemory(0, buffer.Length));
			}
			else
			{
				response.StatusCode = 404;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[PAC] 处理请求时出错: {ex.Message}");
		}
		finally
		{
			try
			{
				response.Close();
			}
			catch
			{
				// Ignore close errors.
			}
		}
	}

	public void Dispose()
	{
		_cancellationTokenSource?.Cancel();
		try { _httpListener?.Stop(); } catch { }
		_httpListener?.Close();
		_cancellationTokenSource?.Dispose();
		_httpListener = null;
		_listenerTask = null;
		_cancellationTokenSource = null;
	}
}
