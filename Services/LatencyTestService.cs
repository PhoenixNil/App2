using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace App2.Services;

/// <summary>
/// 延迟测试结果
/// </summary>
public class LatencyTestResult
{
	/// <summary>
	/// 是否测试成功
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// 延迟时间（毫秒）
	/// </summary>
	public int Latency { get; set; }

	/// <summary>
	/// 错误信息
	/// </summary>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// 状态描述（用于 UI 显示）
	/// </summary>
	public string StatusText => Success ? $"{Latency} ms" : "超时";

	/// <summary>
	/// 延迟等级（用于颜色标识）
	/// </summary>
	public LatencyLevel Level
	{
		get
		{
			if (!Success) return LatencyLevel.Timeout;
			if (Latency < 100) return LatencyLevel.Excellent;
			if (Latency < 200) return LatencyLevel.Good;
			if (Latency < 500) return LatencyLevel.Fair;
			return LatencyLevel.Poor;
		}
	}
}

/// <summary>
/// 延迟等级
/// </summary>
public enum LatencyLevel
{
	/// <summary>
	/// 优秀 (&lt; 100ms)
	/// </summary>
	Excellent,

	/// <summary>
	/// 良好 (100-200ms)
	/// </summary>
	Good,

	/// <summary>
	/// 一般 (200-500ms)
	/// </summary>
	Fair,

	/// <summary>
	/// 较差 (&gt; 500ms)
	/// </summary>
	Poor,

	/// <summary>
	/// 超时/失败
	/// </summary>
	Timeout
}

/// <summary>
/// 负责测试服务器延迟
/// </summary>
public class LatencyTestService
{
	private const int DefaultTimeoutMs = 5000;
	private const int DefaultRetryCount = 3;

	/// <summary>
	/// 测试服务器延迟（TCP 连接测试）
	/// </summary>
	/// <param name="host">服务器地址</param>
	/// <param name="port">服务器端口</param>
	/// <param name="timeoutMs">超时时间（毫秒）</param>
	/// <param name="cancellationToken">取消令牌</param>
	/// <returns>延迟测试结果</returns>
	public async Task<LatencyTestResult> TestLatencyAsync(
		string host,
		int port,
		int timeoutMs = DefaultTimeoutMs,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return new LatencyTestResult
			{
				Success = false,
				ErrorMessage = "主机名为空"
			};
		}

		try
		{
			var stopwatch = Stopwatch.StartNew();

			using var client = new TcpClient();
			
			// 使用超时控制
			var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
			var timeoutTask = Task.Delay(timeoutMs, cancellationToken);
			var completedTask = await Task.WhenAny(connectTask, timeoutTask);

			if (completedTask == timeoutTask || !client.Connected)
			{
				return new LatencyTestResult
				{
					Success = false,
					Latency = timeoutMs,
					ErrorMessage = "连接超时"
				};
			}

			stopwatch.Stop();

			return new LatencyTestResult
			{
				Success = true,
				Latency = (int)stopwatch.ElapsedMilliseconds
			};
		}
		catch (OperationCanceledException)
		{
			return new LatencyTestResult
			{
				Success = false,
				ErrorMessage = "测试已取消"
			};
		}
		catch (SocketException ex)
		{
			return new LatencyTestResult
			{
				Success = false,
				ErrorMessage = $"连接失败: {ex.Message}"
			};
		}
		catch (Exception ex)
		{
			return new LatencyTestResult
			{
				Success = false,
				ErrorMessage = $"测试失败: {ex.Message}"
			};
		}
	}

	/// <summary>
	/// 测试服务器延迟（带重试机制，取最佳结果）
	/// </summary>
	/// <param name="host">服务器地址</param>
	/// <param name="port">服务器端口</param>
	/// <param name="retryCount">重试次数</param>
	/// <param name="timeoutMs">超时时间（毫秒）</param>
	/// <param name="cancellationToken">取消令牌</param>
	/// <returns>延迟测试结果</returns>
	public async Task<LatencyTestResult> TestLatencyWithRetryAsync(
		string host,
		int port,
		int retryCount = DefaultRetryCount,
		int timeoutMs = DefaultTimeoutMs,
		CancellationToken cancellationToken = default)
	{
		LatencyTestResult? bestResult = null;

		for (int i = 0; i < retryCount; i++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var result = await TestLatencyAsync(host, port, timeoutMs, cancellationToken);

			if (result.Success)
			{
				if (bestResult == null || result.Latency < bestResult.Latency)
				{
					bestResult = result;
				}
			}

			// 如果不是最后一次尝试，稍微等待一下
			if (i < retryCount - 1 && !cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(100, cancellationToken);
			}
		}

		return bestResult ?? new LatencyTestResult
		{
			Success = false,
			ErrorMessage = "所有测试均失败"
		};
	}
}

