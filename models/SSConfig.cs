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
}
