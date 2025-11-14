using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace App2.Services;

/// <summary>
/// 提供与主窗口相关的上下文对象，例如用于显示 ContentDialog 的 XamlRoot。
/// </summary>
public interface IWindowContext
{
	/// <summary>
	/// 绑定当前窗口，供服务层使用。
	/// </summary>
	/// <param name="window">正在使用的 WinUI 窗口。</param>
	void Attach(Window window);

	/// <summary>
	/// 获取当前窗口的根元素。
	/// </summary>
	FrameworkElement? RootElement { get; }

	/// <summary>
	/// 获取当前窗口的 XamlRoot，用于构造 ContentDialog。
	/// </summary>
	XamlRoot? XamlRoot { get; }
}
