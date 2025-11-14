namespace App2.Services;

/// <summary>
/// 服务器编辑对话框的输入数据。
/// </summary>
public class ServerDialogResult
{
	public string Name { get; set; } = string.Empty;
	public string Host { get; set; } = string.Empty;
	public string Port { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string Method { get; set; } = string.Empty;
}
