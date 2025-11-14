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

public sealed partial class MainWindow : Window
{
	public MainWindowViewModel ViewModel { get; }

	public MainWindow(MainWindowViewModel viewModel)
	{
		ViewModel = viewModel;
		InitializeComponent();

		// Set window default size (DIP 950x600), considering system DPI scaling
		var hWnd = WindowNative.GetWindowHandle(this);
		var id = Win32Interop.GetWindowIdFromWindow(hWnd);
		var appWindow = AppWindow.GetFromWindowId(id);
		var dpiScale = GetWindowScale(hWnd);
		var widthPx = (int)Math.Round(950 * dpiScale);
		var heightPx = (int)Math.Round(600 * dpiScale);
		appWindow.Resize(new SizeInt32(widthPx, heightPx));

		//Set taskbar Icon;
		string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "output.ico");
		appWindow.SetIcon(iconPath);
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(AppTitleBar);

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
		appWindow.Closing += (sender, args) =>
		{
			// 取消关闭操作
			args.Cancel = true;

			// 最小化并隐藏窗口（不在任务切换器中显示）
			wm.WindowState = WinUIEx.WindowState.Minimized;
			appWindow.Hide();
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
		await ViewModel.CleanupAsync();
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

	#endregion
}
