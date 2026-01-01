using System.Text.Json.Serialization;

namespace App2.Models;

/// <summary>
/// DNS 配置模型（用于 TUN 模式防止 DNS 泄漏）
/// </summary>
public class DnsConfig
{
    [JsonPropertyName("servers")]
    public string[]? Servers { get; set; }
}
