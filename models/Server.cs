using System.Text.Json.Serialization;

namespace App2.Models;

/// <summary>
/// Shadowsocks 服务器配置
/// </summary>
public class Server
{
    [JsonPropertyName("server")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("server_port")]
    public int Port { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = "aes-256-gcm";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
