using System.Text.Json.Serialization;

namespace App2.Models;

/// <summary>
/// JSON 序列化上下文（支持 AOT 和性能优化）
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SSConfig))]
[JsonSerializable(typeof(Server))]
[JsonSerializable(typeof(Server[]))]
public partial class JsonContext : JsonSerializerContext
{
}
