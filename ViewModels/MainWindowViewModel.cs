using System.Threading.Tasks;
using App2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace App2.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	private readonly IThemeService _themeService;

	public MainWindowViewModel(
		ServerListViewModel serverList,
		ControlPanelViewModel controlPanel,
		ServerDetailViewModel serverDetail,
		IThemeService themeService)
	{
		ServerList = serverList;
		ControlPanel = controlPanel;
		ServerDetail = serverDetail;
		_themeService = themeService;

		ServerList.LoadServers();
	}

	public ServerListViewModel ServerList { get; }
	public ControlPanelViewModel ControlPanel { get; }
	public ServerDetailViewModel ServerDetail { get; }

	public ElementTheme CurrentTheme => _themeService.CurrentTheme;

	public void Initialize(DispatcherQueue dispatcherQueue)
	{
		ControlPanel.Initialize(dispatcherQueue);
	}

	public async Task CleanupAsync()
	{
		await ControlPanel.CleanupAsync().ConfigureAwait(false);
		ServerDetail.Dispose();
	}
}
