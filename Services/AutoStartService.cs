using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace App2.Services;

/// <summary>
/// 通过写入注册表管理应用的开机启动状态。
/// </summary>
public class AutoStartService
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private readonly string _valueName;
	private readonly string _executablePath;

	public AutoStartService()
	{
		_valueName = Assembly.GetEntryAssembly()?.GetName().Name ?? "App2";
		_executablePath = Environment.ProcessPath
			?? Process.GetCurrentProcess().MainModule?.FileName
			?? throw new InvalidOperationException("无法确定当前应用程序路径");
	}

	/// <summary>
	/// 当前用户是否启用了开机启动。
	/// </summary>
	public bool IsEnabled()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
		if (key == null)
		{
			return false;
		}

		var value = key.GetValue(_valueName) as string;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var normalizedValue = NormalizePath(value);
		var normalizedExecutable = NormalizePath(_executablePath);
		return string.Equals(normalizedValue, normalizedExecutable, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 启用开机启动。
	/// </summary>
	public void Enable()
	{
		using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
		              ?? throw new InvalidOperationException("无法打开当前用户的开机启动注册表键");
		key.SetValue(_valueName, $"\"{_executablePath}\"", RegistryValueKind.String);
	}

	/// <summary>
	/// 禁用开机启动。
	/// </summary>
	public void Disable()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
		key?.DeleteValue(_valueName, throwOnMissingValue: false);
	}

	private static string NormalizePath(string path)
	{
		var unquoted = path.Trim().Trim('"');
		return Path.GetFullPath(unquoted);
	}
}
