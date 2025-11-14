using Microsoft.UI.Xaml;

namespace App2.Services;

/// <summary>
/// 记录当前窗口，供需要 XamlRoot/RootElement 的服务使用。
/// </summary>
public class WindowContext : IWindowContext
{
	private Window? _window;

	public void Attach(Window window)
	{
		_window = window;
	}

	public FrameworkElement? RootElement => _window?.Content as FrameworkElement;

	public XamlRoot? XamlRoot => RootElement?.XamlRoot;
}
