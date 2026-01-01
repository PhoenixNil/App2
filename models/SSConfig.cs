using System.Text.Json.Serialization;

namespace App2.Models;

/// <summary>
/// sslocal.exe 配置文件模型
/// </summary>
public class SSConfig
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = string.Empty;

    [JsonPropertyName("server_port")]
    public int ServerPort { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = "aes-256-gcm";

    [JsonPropertyName("local_address")]
    public string LocalAddress { get; set; } = "127.0.0.1";

    [JsonPropertyName("local_port")]
    public int LocalPort { get; set; } = 1080;

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 300;

    [JsonPropertyName("acl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ACL { get; set; }

    /// <summary>
    /// DNS 服务器地址（TUN 模式防止 DNS 泄漏）
    /// shadowsocks-rust 期望简单字符串格式，如 "8.8.8.8" 或 "google"
    /// </summary>
    [JsonPropertyName("dns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dns { get; set; }

    /// <summary>
    /// 转发模式：tcp_only, udp_only, tcp_and_udp
    /// TUN 模式需要 tcp_and_udp 才能正常工作
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }
}
