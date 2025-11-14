using Windows.ApplicationModel.DataTransfer;

namespace App2.Services;

/// <summary>
/// 基于 WinUI 剪贴板的默认实现。
/// </summary>
public class ClipboardService : IClipboardService
{
	public void SetText(string text)
	{
		var package = new DataPackage();
		package.SetText(text);
		Clipboard.SetContent(package);
		Clipboard.Flush();
	}
}
