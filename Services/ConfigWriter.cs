using App2.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace App2.Services;

/// <summary>
/// 负责生成和写入 sslocal 配置文件
/// </summary>
public class ConfigWriter : IConfigWriter
{
    private readonly string _configPath;

    public ConfigWriter()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "App2");
        Directory.CreateDirectory(appFolder);
        _configPath = Path.Combine(appFolder, "sslocal.json");

        // 诊断输出
        Debug.WriteLine($"[ConfigWriter] 应用数据目录: {appFolder}");
        Debug.WriteLine($"[ConfigWriter] 配置文件路径: {_configPath}");
    }

    /// <summary>
    /// 根据服务器配置生成 sslocal 配置文件
    /// </summary>
    /// <param name="server">服务器配置</param>
    /// <param name="localPort">本地端口</param>
    /// <param name="aclPath">ACL 文件路径（可选）</param>
    /// <param name="isTunMode">是否为 TUN 模式</param>
    /// <param name="dnsServers">DNS 服务器列表（TUN 模式用于防止 DNS 泄漏）</param>
    public void WriteConfig(ServerEntry server, int localPort = 1080, string? aclPath = null, bool isTunMode = false, string[]? dnsServers = null)
    {
        if (!string.IsNullOrWhiteSpace(aclPath))
        {
            aclPath = Path.GetFullPath(aclPath);
            if (!Path.IsPathRooted(aclPath))
            {
                throw new InvalidOperationException("ACL 路径必须为绝对路径。");
            }
        }

        var config = new SSConfig
        {
            Server = server.Host,
            ServerPort = server.Port,
            Password = server.Password,
            Method = server.Method,
            LocalAddress = "127.0.0.1",
            LocalPort = localPort,
            Timeout = 300,
            ACL = aclPath
        };

        // TUN 模式添加 DNS 配置防止 DNS 泄漏
        // shadowsocks-rust 期望简单字符串格式
        if (isTunMode && dnsServers != null && dnsServers.Length > 0)
        {
            config.Dns = dnsServers[0]; // 使用第一个 DNS 服务器
        }

        // TUN 模式需要同时启用 TCP 和 UDP 转发
        if (isTunMode)
        {
            config.Mode = "tcp_and_udp";
        }

        var json = JsonSerializer.Serialize(config, JsonContext.Default.SSConfig);
        File.WriteAllText(_configPath, json);
        Debug.WriteLine($"[ConfigWriter] 已写入配置: {_configPath}");
        Debug.WriteLine($"[ConfigWriter] ACL: {(string.IsNullOrWhiteSpace(config.ACL) ? "<none>" : config.ACL)}");
        Debug.WriteLine($"[ConfigWriter] TUN: {isTunMode}, DNS: {(string.IsNullOrWhiteSpace(config.Dns) ? "<none>" : config.Dns)}");
    }

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    public string GetConfigPath() => _configPath;

    /// <summary>
    /// 删除配置文件
    /// </summary>
    public void DeleteConfig()
    {
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }
}
