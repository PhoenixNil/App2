using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using Windows.ApplicationModel;

namespace App2.Services;

/// <summary>
/// TUN 模式相关服务，负责网络接口检测和 TUN 配置
/// </summary>
public class TunService
{
    private readonly string _engineDirectory;

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
    public string[] DnsServers => new[] { "8.8.8.8", "1.1.1.1" };

    public TunService()
    {
        // 尝试多个可能的路径来定位 engine 目录
        var possiblePaths = new System.Collections.Generic.List<string>();

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
    public string? DetectOutboundInterface()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                    && !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase)
                    && !ni.Description.Contains("TUN", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(ni => ni.GetIPProperties().GatewayAddresses.Count)
                .ThenByDescending(ni => ni.Speed)
                .ToList();

            // 优先选择有网关的接口（通常是主要的上网接口）
            var primaryInterface = interfaces.FirstOrDefault(ni =>
                ni.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

            return primaryInterface?.Name ?? interfaces.FirstOrDefault()?.Name;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TunService] 检测网络接口失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 检查 wintun.dll 是否存在
    /// </summary>
    public bool IsWintunAvailable()
    {
        var wintunPath = Path.Combine(_engineDirectory, "wintun.dll");
        var exists = File.Exists(wintunPath);
        System.Diagnostics.Debug.WriteLine($"[TunService] wintun.dll 路径: {wintunPath}, 存在: {exists}");
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
    /// TUN 网关地址（从 DefaultTunInterfaceAddress 提取）
    /// </summary>
    public string TunGateway => "10.255.0.1";

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
            System.Diagnostics.Debug.WriteLine($"[TunService] 获取默认网关失败: {ex.Message}");
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
                return ipv4Props?.Index;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TunService] 获取 TUN 接口索引失败: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("[TunService] 无法获取原始网关，跳过路由设置");
                return false;
            }

            // 获取 TUN 接口索引
            var tunInterfaceIndex = GetTunInterfaceIndex();
            if (tunInterfaceIndex == null)
            {
                System.Diagnostics.Debug.WriteLine("[TunService] 无法获取 TUN 接口索引，跳过路由设置");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[TunService] 原始网关: {originalGateway}");
            System.Diagnostics.Debug.WriteLine($"[TunService] SS 服务器: {serverAddress}");
            System.Diagnostics.Debug.WriteLine($"[TunService] TUN 接口索引: {tunInterfaceIndex}");

            // 1. 为 SS 服务器添加直连路由（通过原网关）
            RunRouteCommand($"add {serverAddress} mask 255.255.255.255 {originalGateway} metric 5");

            // 2. 添加 0.0.0.0/1 和 128.0.0.0/1 路由，覆盖默认路由但不删除它
            // 使用 if 参数指定 TUN 接口索引，确保流量走 TUN
            RunRouteCommand($"add 0.0.0.0 mask 128.0.0.0 {TunGateway} metric 5 if {tunInterfaceIndex}");
            RunRouteCommand($"add 128.0.0.0 mask 128.0.0.0 {TunGateway} metric 5 if {tunInterfaceIndex}");

            System.Diagnostics.Debug.WriteLine("[TunService] TUN 路由设置完成");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TunService] 设置 TUN 路由失败: {ex.Message}");
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
            // 删除我们添加的路由
            RunRouteCommand($"delete 0.0.0.0 mask 128.0.0.0");
            RunRouteCommand($"delete 128.0.0.0 mask 128.0.0.0");

            if (!string.IsNullOrEmpty(serverAddress))
            {
                RunRouteCommand($"delete {serverAddress}");
            }

            System.Diagnostics.Debug.WriteLine("[TunService] TUN 路由清理完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TunService] 清理 TUN 路由失败: {ex.Message}");
        }
    }

    private void RunRouteCommand(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "route",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
            System.Diagnostics.Debug.WriteLine($"[TunService] route {arguments} - 退出代码: {process?.ExitCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TunService] route 命令执行失败: {ex.Message}");
        }
    }
}
