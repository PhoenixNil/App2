namespace App2.Services;

/// <summary>
/// 剪贴板访问接口，便于在 ViewModel 中解耦 UI 实现。
/// </summary>
public interface IClipboardService
{
	void SetText(string text);
}
