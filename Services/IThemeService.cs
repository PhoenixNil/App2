using System;
using Microsoft.UI.Xaml;

namespace App2.Services;

public interface IThemeService
{
	ElementTheme CurrentTheme { get; }
	ElementTheme ActualTheme { get; }
	event EventHandler? ThemeChanged;
	void ApplyTheme(ElementTheme theme);
}
