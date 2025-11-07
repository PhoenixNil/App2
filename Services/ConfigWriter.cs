using App2.Models;
using System;
using System.IO;
using System.Text.Json;

namespace App2.Services;

/// <summary>
/// 负责生成和写入 sslocal 配置文件
/// </summary>
public class ConfigWriter
{
    private readonly string _configPath;

    public ConfigWriter()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "App2");
        Directory.CreateDirectory(appFolder);
        _configPath = Path.Combine(appFolder, "sslocal.json");

        // 诊断输出
        System.Diagnostics.Debug.WriteLine($"[ConfigWriter] 应用数据目录: {appFolder}");
        System.Diagnostics.Debug.WriteLine($"[ConfigWriter] 配置文件路径: {_configPath}");
    }

    /// <summary>
    /// 根据服务器配置生成 sslocal 配置文件
    /// </summary>
    /// <param name="server">服务器配置</param>
    /// <param name="localPort">本地端口</param>
    /// <param name="aclPath">ACL 文件路径（可选）</param>
    public void WriteConfig(ServerEntry server, int localPort = 1080, string? aclPath = null)
    {
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

        var json = JsonSerializer.Serialize(config, JsonContext.Default.SSConfig);
        File.WriteAllText(_configPath, json);
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
