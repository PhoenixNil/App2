using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using App2.Helpers;
using Windows.ApplicationModel;

namespace App2.Services;

/// <summary>
/// TUN 模式相关服务，负责网络接口检测和 TUN 配置
/// </summary>
public class TunService : ITunService
{
    private readonly string _engineDirectory;
    private const int OutboundInterfaceCacheSeconds = 30;
    private string? _cachedOutboundInterface;
    private DateTimeOffset _cachedOutboundInterfaceAt;

    /// <summary>
    /// 默认 TUN 接口名称
    /// </summary>
    public string DefaultTunInterfaceName => "shadowsocks-tun";

    /// <summary>
    /// 默认 TUN 接口地址（CIDR 格式）
    /// 这个地址用于配置虚拟网卡的 IP 和网段
    /// </summary>
    public string DefaultTunInterfaceAddress => "10.255.0.1/24";

    /// <summary>
    /// DNS 服务器列表（用于防止 DNS 泄漏）
    /// </summary>
    public string[] DnsServers => ["223.5.5.5", "119.29.29.29"];

    public TunService()
    {
        // 尝试多个可能的路径来定位 engine 目录
        var possiblePaths = new List<string>();

        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                possiblePaths.Add(Path.Combine(Package.Current.InstalledPath, "Assets", "engine"));
            }
        }
        catch
        {
            // 忽略，继续尝试其他路径
        }

        possiblePaths.Add(Path.Combine(AppContext.BaseDirectory, "Assets", "engine"));
        possiblePaths.Add(AppContext.BaseDirectory);

        _engineDirectory = possiblePaths.FirstOrDefault(Directory.Exists) ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// 检测主要的出站网络接口名称
    /// </summary>
    /// <returns>网络接口名称，如果检测失败则返回 null</returns>
    public string? DetectOutboundInterface(bool forceRefresh = false)
    {
        try
        {
            if (!forceRefresh && TryGetCachedOutboundInterface(out var cached))
            {
                return cached;
            }

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                    && !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("TUN", StringComparison.OrdinalIgnoreCase))
                .Select(ni => (Interface: ni, Props: ni.GetIPProperties()))
                .OrderByDescending(x => x.Props.GatewayAddresses.Count)
                .ThenByDescending(x => x.Interface.Speed)
                .ToList();

            // 优先选择有网关的接口（通常是主要的上网接口）
            var primary = interfaces.FirstOrDefault(x =>
                x.Props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

            var detected = primary.Interface?.Name ?? interfaces.FirstOrDefault().Interface?.Name;
            CacheOutboundInterface(detected);
            return detected;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] 检测网络接口失败: {ex.Message}");
            return null;
        }
    }

    private bool TryGetCachedOutboundInterface(out string? cached)
    {
        cached = _cachedOutboundInterface;
        if (string.IsNullOrWhiteSpace(cached))
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - _cachedOutboundInterfaceAt;
        return age <= TimeSpan.FromSeconds(OutboundInterfaceCacheSeconds);
    }

    private void CacheOutboundInterface(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _cachedOutboundInterface = value;
        _cachedOutboundInterfaceAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 检查 wintun.dll 是否存在
    /// </summary>
    public bool IsWintunAvailable()
    {
        var wintunPath = Path.Combine(_engineDirectory, "wintun.dll");
        var exists = File.Exists(wintunPath);
        Debug.WriteLine($"[TunService] wintun.dll 路径: {wintunPath}, 存在: {exists}");
        return exists;
    }

    /// <summary>
    /// 获取 wintun.dll 的预期路径（用于错误提示）
    /// </summary>
    public string GetExpectedWintunPath()
    {
        return Path.Combine(_engineDirectory, "wintun.dll");
    }

    /// <summary>
    /// 获取所有可用的网络接口列表
    /// </summary>
    public string[] GetAvailableInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(ni => ni.Name)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// TUN 网关地址（从 DefaultTunInterfaceAddress 的 CIDR 前缀提取）
    /// </summary>
    public string TunGateway => DefaultTunInterfaceAddress.Split('/')[0];

    /// <summary>
    /// 获取当前默认网关地址
    /// </summary>
    public string? GetDefaultGateway()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            foreach (var ni in interfaces)
            {
                var gateway = ni.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (gateway != null)
                {
                    return gateway.Address.ToString();
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] 获取默认网关失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取 TUN 接口的索引号
    /// </summary>
    public int? GetTunInterfaceIndex(string tunInterfaceName = "shadowsocks-tun")
    {
        try
        {
            var tunInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(ni => ni.Name.Equals(tunInterfaceName, StringComparison.OrdinalIgnoreCase)
                    || ni.Description.Contains(tunInterfaceName, StringComparison.OrdinalIgnoreCase));

            if (tunInterface != null)
            {
                var ipProps = tunInterface.GetIPProperties();
                var ipv4Props = ipProps.GetIPv4Properties();
                return ipv4Props.Index;
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] 获取 TUN 接口索引失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设置 TUN 路由（将所有流量路由到 TUN 接口）
    /// </summary>
    /// <param name="serverAddress">SS 服务器地址（需要保持原路由）</param>
    /// <returns>是否成功</returns>
    public bool SetupTunRoutes(string serverAddress)
    {
        try
        {
            var originalGateway = GetDefaultGateway();
            if (string.IsNullOrEmpty(originalGateway))
            {
                Debug.WriteLine("[TunService] 无法获取原始网关，跳过路由设置");
                return false;
            }

            // 获取 TUN 接口索引
            var tunInterfaceIndex = GetTunInterfaceIndex();
            if (tunInterfaceIndex == null)
            {
                Debug.WriteLine("[TunService] 无法获取 TUN 接口索引，跳过路由设置");
                return false;
            }

            Debug.WriteLine($"[TunService] 原始网关: {originalGateway}");
            Debug.WriteLine($"[TunService] SS 服务器: {serverAddress}");
            Debug.WriteLine($"[TunService] TUN 接口索引: {tunInterfaceIndex}");

            // 1. 为 SS 服务器添加直连路由（通过原网关）
            if (!IsSafeRouteTarget(serverAddress))
            {
                Debug.WriteLine("[TunService] 服务器地址包含非法字符，已阻止路由设置");
                return false;
            }

            var commands = new[]
            {
                $"add {serverAddress} mask 255.255.255.255 {originalGateway} metric 5",
                $"add 0.0.0.0 mask 128.0.0.0 {TunGateway} metric 5 if {tunInterfaceIndex}",
                $"add 128.0.0.0 mask 128.0.0.0 {TunGateway} metric 5 if {tunInterfaceIndex}"
            };

            if (!RunRouteCommands(commands))
            {
                Debug.WriteLine("[TunService] TUN 路由设置失败");
                return false;
            }

            Debug.WriteLine("[TunService] TUN 路由设置完成");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] 设置 TUN 路由失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清理 TUN 路由
    /// </summary>
    /// <param name="serverAddress">SS 服务器地址</param>
    public void CleanupTunRoutes(string? serverAddress)
    {
        try
        {
            var commands = new List<string>
            {
                "delete 0.0.0.0 mask 128.0.0.0",
                "delete 128.0.0.0 mask 128.0.0.0"
            };

            if (!string.IsNullOrEmpty(serverAddress) && IsSafeRouteTarget(serverAddress))
            {
                commands.Add($"delete {serverAddress}");
            }

            RunRouteCommands(commands);

            Debug.WriteLine("[TunService] TUN 路由清理完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] 清理 TUN 路由失败: {ex.Message}");
        }
    }

    private static bool IsSafeRouteTarget(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || ch is '&' or '|' or '>' or '<' or '^')
            {
                return false;
            }
        }

        return true;
    }

    private bool RunRouteCommands(IReadOnlyList<string> commands)
    {
        if (commands.Count == 0)
        {
            return true;
        }

        return AdminHelper.IsAdministrator()
            ? RunRouteCommandsDirect(commands)
            : RunRouteCommandsElevated(commands);
    }

    private bool RunRouteCommandsDirect(IReadOnlyList<string> commands)
    {
        var success = true;
        foreach (var command in commands)
        {
            success &= RunRouteCommandInternal(command);
        }

        return success;
    }

    private bool RunRouteCommandsElevated(IReadOnlyList<string> commands)
    {
        var commandLine = string.Join(" & ", commands.Select(command => $"route {command}"));
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        var psi = new ProcessStartInfo
        {
            FileName = cmdPath,
            Arguments = "/c " + commandLine,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(5000);
            Debug.WriteLine($"[TunService] route 批处理退出代码: {process.ExitCode}");
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Debug.WriteLine("[TunService] 管理员授权被取消");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] route 执行失败: {ex.Message}");
            return false;
        }
    }

    private bool RunRouteCommandInternal(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "route",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            Debug.WriteLine($"[TunService] route {arguments} - 退出代码: {process?.ExitCode}");
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TunService] route 命令执行失败: {ex.Message}");
            return false;
        }
    }
}
