using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace App2.Services;

/// <summary>
/// 代理模式
/// </summary>
public enum ProxyMode
{
	/// <summary>
	/// 直连模式（不使用代理）
	/// </summary>
	Direct,

	/// <summary>
	/// 全局代理模式
	/// </summary>
	Global,

	/// <summary>
	/// PAC 模式（自动配置脚本）
	/// </summary>
	PAC
}

/// <summary>
/// 负责设置和清除系统代理
/// </summary>
public class ProxyService
{
	private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

	[DllImport("wininet.dll", SetLastError = true)]
	private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

	private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
	private const int INTERNET_OPTION_REFRESH = 37;

	private string _proxyServer = "127.0.0.1:1080";
	private ProxyMode _currentMode = ProxyMode.Direct;

	/// <summary>
	/// 设置代理服务器地址
	/// </summary>
	public void SetProxyServer(string host, int port)
	{
		_proxyServer = $"{host}:{port}";
	}

	/// <summary>
	/// 设置代理模式
	/// </summary>
	public void SetProxyMode(ProxyMode mode)
	{
		_currentMode = mode;

		switch (mode)
		{
			case ProxyMode.Direct:
				DisableProxy();
				break;
			case ProxyMode.Global:
				EnableGlobalProxy();
				break;
			case ProxyMode.PAC:
				EnablePACProxy();
				break;
		}

		NotifySystemProxyChanged();
	}

	/// <summary>
	/// 获取当前代理模式
	/// </summary>
	public ProxyMode GetCurrentMode() => _currentMode;

	/// <summary>
	/// 启用全局代理
	/// </summary>
	private void EnableGlobalProxy()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
		if (key == null) return;

		// Shadowsocks 使用 SOCKS5 代理
		key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
		key.SetValue("ProxyServer", $"socks={_proxyServer}", RegistryValueKind.String);
		key.SetValue("ProxyOverride", "localhost;127.*;10.*;172.16.*;172.31.*;192.168.*;<local>", RegistryValueKind.String);
	}

	/// <summary>
	/// 启用 PAC 代理
	/// </summary>
	private void EnablePACProxy()
	{
		// PAC 模式需要一个 PAC 文件的 URL
		// 这里简化实现，暂时使用全局代理
		// 实际项目中需要生成并托管一个 PAC 文件
		using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
		if (key == null) return;

		key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
		key.SetValue("ProxyServer", $"socks={_proxyServer}", RegistryValueKind.String);
		key.SetValue("ProxyOverride", "localhost;127.*;10.*;172.16.*;172.31.*;192.168.*;<local>", RegistryValueKind.String);

		// 如果有 PAC 文件，可以设置：
		// key.SetValue("AutoConfigURL", pacUrl, RegistryValueKind.String);
	}

	/// <summary>
	/// 禁用代理
	/// </summary>
	private void DisableProxy()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
		if (key == null) return;

		key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
		key.DeleteValue("ProxyServer", false);
		key.DeleteValue("AutoConfigURL", false);
	}

	/// <summary>
	/// 通知系统代理设置已更改
	/// </summary>
	private void NotifySystemProxyChanged()
	{
		InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
		InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
	}

	/// <summary>
	/// 清除所有代理设置（程序退出时调用）
	/// </summary>
	public void ClearProxy()
	{
		DisableProxy();
		NotifySystemProxyChanged();
	}
}

