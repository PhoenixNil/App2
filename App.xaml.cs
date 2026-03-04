using App2.Services;
using App2.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace App2;

public partial class App : Application
{
	private Window? _window;

	public App()
	{
		InitializeComponent();
		Services = ConfigureServices();
	}

	public IServiceProvider Services { get; }

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = Services.GetRequiredService<MainWindow>();
		var windowContext = Services.GetRequiredService<IWindowContext>();
		windowContext.Attach(_window);
		var themeService = Services.GetRequiredService<IThemeService>();
		themeService.ApplyTheme(themeService.CurrentTheme);
		_window.Activate();
	}

	private static IServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		services.AddSingleton<IWindowContext, WindowContext>();
		services.AddSingleton<IClipboardService, ClipboardService>();
		services.AddSingleton<IThemeService, ThemeService>();
		services.AddSingleton<IDialogService>(provider => new DialogService(
			provider.GetRequiredService<IWindowContext>(),
			provider.GetRequiredService<IClipboardService>(),
			provider.GetRequiredService<IThemeService>()));
		services.AddSingleton<IConfigWriter, ConfigWriter>();
		services.AddSingleton<AutoStartService>();
		services.AddSingleton<IEngineService, EngineService>();
		services.AddSingleton<IProxyService, ProxyService>();
		services.AddSingleton(provider => new PACServerService(7090));
		services.AddSingleton<IConfigStorage, ConfigStorage>();
		services.AddSingleton<LatencyTestService>();
		services.AddSingleton<ITunService, TunService>();
		services.AddSingleton<IAclService, AclService>();

		services.AddSingleton<ServerListViewModel>();
		services.AddSingleton(provider => new ControlPanelViewModel(
			provider.GetRequiredService<IConfigWriter>(),
			provider.GetRequiredService<IEngineService>(),
			provider.GetRequiredService<IProxyService>(),
			provider.GetRequiredService<PACServerService>(),
			provider.GetRequiredService<IDialogService>(),
			provider.GetRequiredService<IThemeService>(),
			provider.GetRequiredService<AutoStartService>(),
			provider.GetRequiredService<ITunService>(),
			provider.GetRequiredService<IAclService>(),
			provider.GetRequiredService<ServerListViewModel>()));
		services.AddSingleton(provider => new ServerDetailViewModel(
			provider.GetRequiredService<ServerListViewModel>(),
			provider.GetRequiredService<LatencyTestService>()));
		services.AddSingleton<MainWindowViewModel>();
		services.AddSingleton<MainWindow>();

		return services.BuildServiceProvider();
	}
}
