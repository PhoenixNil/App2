using App2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace App2.Services;

/// <summary>
/// 负责持久化保存和加载服务器列表
/// </summary>
public class ConfigStorage
{
    private readonly string _storagePath;

    public ConfigStorage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "App2");
        Directory.CreateDirectory(appFolder);
        _storagePath = Path.Combine(appFolder, "servers.json");

        // 诊断输出
        System.Diagnostics.Debug.WriteLine($"[ConfigStorage] 应用数据目录: {appFolder}");
        System.Diagnostics.Debug.WriteLine($"[ConfigStorage] 服务器列表路径: {_storagePath}");
    }

    /// <summary>
    /// 保存服务器列表
    /// </summary>
    public void SaveServers(IEnumerable<ServerEntry> servers)
    {
        try
        {
            var serverModels = servers.Select(s => new Server
            {
                Name = s.Name,
                Host = s.Host,
                Port = s.Port,
                Method = s.Method,
                Password = s.Password
            }).ToArray();

            var json = JsonSerializer.Serialize(serverModels, JsonContext.Default.ServerArray);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存服务器列表失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 加载服务器列表
    /// </summary>
    public List<ServerEntry> LoadServers()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new List<ServerEntry>();
            }

            var json = File.ReadAllText(_storagePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ServerEntry>();
            }

            var serverModels = JsonSerializer.Deserialize(json, JsonContext.Default.ServerArray);
            if (serverModels == null)
            {
                return new List<ServerEntry>();
            }

            return serverModels.Select(s => new ServerEntry
            {
                Name = s.Name,
                Host = s.Host,
                Port = s.Port,
                Method = s.Method,
                Password = s.Password
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载服务器列表失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 清除所有保存的数据
    /// </summary>
    public void ClearAll()
    {
        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
    }
}
