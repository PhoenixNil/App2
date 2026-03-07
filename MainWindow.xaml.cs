using App2.Services;
using App2.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;
using System.IO;
using WinUIEx;
using Microsoft.UI;

namespace App2;

public sealed partial class MainWindow
{
	private readonly AppWindow _appWindow;
	private readonly IThemeService _themeService;

	public MainWindowViewModel ViewModel { get; }

	public MainWindow(MainWindowViewModel viewModel, IThemeService themeService)
	{
		ViewModel = viewModel;
		_themeService = themeService;
		InitializeComponent();

		// Set window default size (DIP 950x600), considering system DPI scaling
		var hWnd = WindowNative.GetWindowHandle(this);
		var id = Win32Interop.GetWindowIdFromWindow(hWnd);
		_appWindow = AppWindow.GetFromWindowId(id);
		var dpiScale = GetWindowScale(hWnd);
		var widthPx = (int)Math.Round(950 * dpiScale);
		var heightPx = (int)Math.Round(600 * dpiScale);
		_appWindow.Resize(new SizeInt32(widthPx, heightPx));

		//Set taskbar Icon;
		string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "output.ico");
		_appWindow.SetIcon(iconPath);
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(AppTitleBar);
		_themeService.ThemeChanged += ThemeServiceOnThemeChanged;
		UpdateTitleBarTheme(_themeService.ActualTheme);

		//配置系统托盘（通过WindowManager，不需要继承WindowEx）
		SetupTrayIcon();

		Closed += MainWindow_Closed;

		// Initialize ViewModel after window is loaded, passing DispatcherQueue
		ViewModel.Initialize(DispatcherQueue);
	}

	private void SetupTrayIcon()
	{
		var wm = WindowManager.Get(this);
		
		// 显示系统托盘图标
		wm.IsVisibleInTray = true;
		var appWindow = wm.AppWindow;
		appWindow.Closing += (_, args) =>
		{
			// 取消关闭操作
			args.Cancel = true;

			// 最小化并隐藏窗口（不在任务切换器中显示）
			wm.WindowState = WinUIEx.WindowState.Minimized;
			appWindow.Hide();

			// 强制清理工作集内存，大幅降低后台运行时的内存占用
			try
			{
				using var process = System.Diagnostics.Process.GetCurrentProcess();
				SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"清理工作集内存失败: {ex.Message}");
			}
		};

		// 添加系统托盘右键菜单
		wm.TrayIconContextMenu += (w, e) =>
		{
			var flyout = new MenuFlyout();
			
			var openItem = new MenuFlyoutItem { Text = "打开窗口" };
			openItem.Click += (s, args) => 
			{
				Activate();
				wm.WindowState = WinUIEx.WindowState.Normal;
			};
			flyout.Items.Add(openItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			var exitItem = new MenuFlyoutItem { Text = "退出" };
			exitItem.Click += (s, args) => Close();
			flyout.Items.Add(exitItem);

			e.Flyout = flyout;
		};
	}

	private async void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		_themeService.ThemeChanged -= ThemeServiceOnThemeChanged;
		await ViewModel.CleanupAsync();
	}

	private void ThemeServiceOnThemeChanged(object? sender, EventArgs e)
	{
		UpdateTitleBarTheme(_themeService.ActualTheme);
	}

	private void UpdateTitleBarTheme(ElementTheme actualTheme)
	{
		if (!AppWindowTitleBar.IsCustomizationSupported())
		{
			return;
		}

		_appWindow.TitleBar.PreferredTheme = actualTheme switch
		{
			ElementTheme.Light => TitleBarTheme.Light,
			ElementTheme.Dark => TitleBarTheme.Dark,
			_ => TitleBarTheme.UseDefaultAppMode
		};
	}







	#region Helper Methods

	private static double GetWindowScale(IntPtr hwnd)
	{
		try
		{
			if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
			{
				var dpi = GetDpiForWindow(hwnd);
				if (dpi > 0)
				{
					return dpi / 96.0;
				}
			}
		}
		catch
		{
			// ignore and fallback
		}

		return 1.0;
	}

	[DllImport("user32.dll")]
	private static extern int GetDpiForWindow(IntPtr hWnd);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

	#endregion
}


