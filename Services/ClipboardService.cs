using System;
using Windows.ApplicationModel.DataTransfer;

namespace App2.Services;

/// <summary>
/// 基于 WinUI 剪贴板的默认实现。
/// </summary>
public class ClipboardService : IClipboardService
{
	public void SetText(string text)
	{
		try
		{
			var package = new DataPackage();
			package.SetText(text);
			Clipboard.SetContent(package);
			Clipboard.Flush();
		}
		catch (Exception)
		{
			// 剪贴板被占用或不可用时静默处理，避免崩溃
		}
	}
}
