using System;
using Microsoft.UI.Xaml;

namespace App2.Services;

public class ThemeService : IThemeService
{
	private readonly IWindowContext _windowContext;
	private bool _isSubscribed;

	public ThemeService(IWindowContext windowContext)
	{
		_windowContext = windowContext;
	}

	public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;
	public ElementTheme ActualTheme { get; private set; } = ElementTheme.Default;
	public event EventHandler? ThemeChanged;

	public void ApplyTheme(ElementTheme theme)
	{
		CurrentTheme = theme;
		var root = _windowContext.RootElement;
		if (root != null)
		{
			root.RequestedTheme = theme;
			SubscribeActualThemeChanged(root);
		}

		UpdateActualTheme();
		ThemeChanged?.Invoke(this, EventArgs.Empty);
	}

	private void SubscribeActualThemeChanged(FrameworkElement root)
	{
		if (_isSubscribed)
		{
			return;
		}

		root.ActualThemeChanged += RootOnActualThemeChanged;
		_isSubscribed = true;
	}

	private void RootOnActualThemeChanged(FrameworkElement sender, object args)
	{
		UpdateActualTheme();
		ThemeChanged?.Invoke(this, EventArgs.Empty);
	}

	private void UpdateActualTheme()
	{
		var actual = _windowContext.RootElement?.ActualTheme ?? ElementTheme.Default;
		if (ActualTheme != actual)
		{
			ActualTheme = actual;
		}
	}
}
