using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App2.Models;
using App2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App2.ViewModels;

public partial class ServerListViewModel : ObservableObject
{
	private readonly ConfigStorage _configStorage;
	private readonly IDialogService _dialogService;

	private static readonly string[] DefaultMethods = new[]
	{
		"aes-128-gcm",
		"aes-256-gcm",
		"chacha20-ietf-poly1305",
		"2022-blake3-aes-256-gcm",
		"2022-blake3-aes-128-gcm",
		"2022-blake3-chacha20-poly1305"
	};

	public ObservableCollection<ServerEntry> Servers { get; } = new();

	private ServerEntry? _selectedServer;
	public ServerEntry? SelectedServer
	{
		get => _selectedServer;
		set
		{
			if (SetProperty(ref _selectedServer, value))
			{
				SelectedServerChanged?.Invoke(this, EventArgs.Empty);
				RaiseCommandStates();
			}
		}
	}

	private ServerEntry? _activeServer;
	public ServerEntry? ActiveServer
	{
		get => _activeServer;
		private set
		{
			if (SetProperty(ref _activeServer, value))
			{
				UpdateActiveStates();
			}
		}
	}

	private bool _isRunning;
	public bool IsRunning
	{
		get => _isRunning;
		set
		{
			if (SetProperty(ref _isRunning, value))
			{
				RaiseCommandStates();
			}
		}
	}

	public bool CanEditServer => !IsRunning && SelectedServer != null;
	public bool CanRemoveServer => !IsRunning && SelectedServer != null;

public event EventHandler? SelectedServerChanged;

public ServerListViewModel(ConfigStorage configStorage, IDialogService dialogService)
{
	_configStorage = configStorage;
	_dialogService = dialogService;

	Servers.CollectionChanged += OnServersCollectionChanged;
}

	public void LoadServers()
	{
		var loaded = _configStorage.LoadServers();
		Servers.CollectionChanged -= OnServersCollectionChanged;
		Servers.Clear();
		foreach (var server in loaded)
		{
			Servers.Add(server);
		}
		Servers.CollectionChanged += OnServersCollectionChanged;

		if (Servers.Count > 0)
		{
			SelectedServer = Servers[0];
		}
	}

	public IReadOnlyList<ServerEntry> SearchServers(string query)
	{
		return Servers
			.Where(server => !string.IsNullOrEmpty(server.Name) && server.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}

	public void SetActiveServer(ServerEntry? server)
	{
		ActiveServer = server;
	}

[RelayCommand]
private async Task ImportFromLinkAsync()
	{
		var input = await _dialogService.PromptForTextAsync("从 SS 链接导入", "粘贴 SS 链接 (ss://...)", "支持标准 ss:// 链接。", multiline: true);
		if (string.IsNullOrWhiteSpace(input))
		{
			return;
		}

		var entry = ParseSSUrl(input.Trim());
		if (entry == null)
		{
			await _dialogService.ShowErrorAsync("导入失败", "无法解析该 SS 链接，请确认格式是否正确。");
			return;
		}

		Servers.Add(entry);
		SelectedServer = entry;
	}

[RelayCommand]
private async Task AddManualAsync()
	{
		await ShowEditorAsync("手动添加服务器", null);
	}

private bool CanModifySelectedServer() => SelectedServer != null && !IsRunning;

[RelayCommand(CanExecute = nameof(CanModifySelectedServer))]
private async Task EditServerAsync()
	{
		if (SelectedServer == null)
		{
			return;
		}

		var defaults = new ServerDialogResult
		{
			Name = SelectedServer.Name,
			Host = SelectedServer.Host,
			Port = SelectedServer.Port.ToString(),
			Password = SelectedServer.Password,
			Method = SelectedServer.Method
		};

		await ShowEditorAsync("编辑服务器", defaults, SelectedServer);
	}

[RelayCommand(CanExecute = nameof(CanModifySelectedServer))]
private async Task RemoveServerAsync()
	{
		if (SelectedServer == null)
		{
			return;
		}

		var confirmed = await _dialogService.ShowConfirmationAsync("确认删除", $"确定要删除 {SelectedServer.Name}?", "删除", "取消", isDanger: true);
		if (!confirmed)
		{
			return;
		}

		var index = Servers.IndexOf(SelectedServer);
		Servers.Remove(SelectedServer);

		if (Servers.Count > 0)
		{
			var targetIndex = Math.Min(index, Servers.Count - 1);
			SelectedServer = Servers[targetIndex];
		}
		else
		{
			SelectedServer = null;
		}
	}

	private async Task ShowEditorAsync(string title, ServerDialogResult? defaults, ServerEntry? target = null)
	{
		string? Validator(ServerDialogResult input)
		{
			try
			{
				_ = CreateServerEntry(input.Name, input.Host, input.Port, input.Password, input.Method);
				return null;
			}
			catch (ArgumentException ex)
			{
				return ex.Message;
			}
		}

		var dialogResult = await _dialogService.ShowServerEditorAsync(title, defaults, DefaultMethods, Validator);
		if (dialogResult == null)
		{
			return;
		}

		var entry = CreateServerEntry(dialogResult.Name, dialogResult.Host, dialogResult.Port, dialogResult.Password, dialogResult.Method);

		if (target == null)
		{
			Servers.Add(entry);
			SelectedServer = entry;
		}
		else
		{
			target.Name = entry.Name;
			target.Host = entry.Host;
			target.Port = entry.Port;
			target.Password = entry.Password;
			target.Method = entry.Method;
			target.OnPropertyChanged(nameof(ServerEntry.Name));
			target.OnPropertyChanged(nameof(ServerEntry.Host));
			target.OnPropertyChanged(nameof(ServerEntry.Port));
			target.OnPropertyChanged(nameof(ServerEntry.Method));
			SelectedServer = target;
		}
	}

	private void OnServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		try
		{
			_configStorage.SaveServers(Servers);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"保存服务器列表失败: {ex.Message}");
		}

		if (Servers.Count == 0)
		{
			ActiveServer = null;
		}
	}

	private void UpdateActiveStates()
	{
		foreach (var server in Servers)
		{
			server.IsActive = server == ActiveServer;
		}
	}

	private void RaiseCommandStates()
	{
		OnPropertyChanged(nameof(CanEditServer));
		OnPropertyChanged(nameof(CanRemoveServer));
		EditServerCommand.NotifyCanExecuteChanged();
		RemoveServerCommand.NotifyCanExecuteChanged();
	}

	public ServerEntry? ParseSSUrl(string ssUrl)
	{
		if (string.IsNullOrWhiteSpace(ssUrl) || !ssUrl.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var content = ssUrl[5..].Trim();
		var breakIndex = content.IndexOfAny(new[] { '\r', '\n', '\t', ' ' });
		if (breakIndex >= 0) content = content[..breakIndex];
		if (content.Length == 0) return null;

		string? remark = null;
		var remarkIndex = content.IndexOf('#');
		if (remarkIndex >= 0)
		{
			var remarkPart = content[(remarkIndex + 1)..];
			if (!string.IsNullOrEmpty(remarkPart)) remark = DecodeUriComponent(remarkPart);
			content = content[..remarkIndex];
		}

		var queryIndex = content.IndexOf('?');
		if (queryIndex >= 0) content = content[..queryIndex];

		content = DecodeUriComponent(content).Trim();
		if (content.Length == 0) return null;

		if (TryDecodeBase64(content, out var decodedWhole) && TryParseProfile(decodedWhole, out var entryA))
		{
			return FinalizeEntry(entryA, remark);
		}

		var at = content.LastIndexOf('@');
		if (at > 0 && at < content.Length - 1)
		{
			var left = content[..at];
			var right = content[(at + 1)..];

			if (TryDecodeBase64(left, out var userinfoDecoded) && userinfoDecoded.Contains(':'))
			{
				var profile = $"{userinfoDecoded}@{DecodeUriComponent(right)}";
				if (TryParseProfile(profile, out var entryB))
				{
					return FinalizeEntry(entryB, remark);
				}
			}
		}

		var plain = DecodeUriComponent(content);
		return TryParseProfile(plain, out var entryC) ? FinalizeEntry(entryC, remark) : null;

		static ServerEntry FinalizeEntry(ServerEntry entry, string? remarkText)
		{
			if (!string.IsNullOrWhiteSpace(remarkText))
			{
				entry.Name = remarkText;
			}
			else if (string.IsNullOrWhiteSpace(entry.Name))
			{
				entry.Name = $"{entry.Host}:{entry.Port}";
			}
			return entry;
		}
	}

	private ServerEntry CreateServerEntry(string name, string host, string port, string password, string method)
	{
		if (!int.TryParse(port, out var portValue) || portValue <= 0 || portValue > 65535)
		{
			throw new ArgumentException("端口号无效，请输入 1-65535 之间的数字。");
		}

		if (string.IsNullOrWhiteSpace(host))
		{
			throw new ArgumentException("服务器地址不能为空。");
		}

		if (string.IsNullOrWhiteSpace(method))
		{
			method = "aes-256-gcm";
		}

		return new ServerEntry
		{
			Name = string.IsNullOrWhiteSpace(name) ? "未命名节点" : name.Trim(),
			Host = host.Trim(),
			Port = portValue,
			Password = password.Trim(),
			Method = method.Trim()
		};
	}

	private static bool TryDecodeBase64(string payload, out string result)
	{
		result = string.Empty;
		if (string.IsNullOrWhiteSpace(payload)) return false;

		var normalized = payload.Trim().Replace('-', '+').Replace('_', '/');
		var remainder = normalized.Length % 4;
		if (remainder == 1) return false;
		if (remainder > 0) normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');

		try
		{
			var data = Convert.FromBase64String(normalized);
			result = Encoding.UTF8.GetString(data);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryParseProfile(string profile, out ServerEntry entry)
	{
		entry = null!;
		if (string.IsNullOrWhiteSpace(profile))
		{
			return false;
		}

		profile = profile.Trim().TrimEnd('/');
		var atIndex = profile.LastIndexOf('@');
		if (atIndex <= 0 || atIndex == profile.Length - 1)
		{
			return false;
		}

		var methodAndPassword = profile[..atIndex];
		var hostAndPort = profile[(atIndex + 1)..];

		var colonIndex = methodAndPassword.IndexOf(':');
		if (colonIndex <= 0 || colonIndex == methodAndPassword.Length - 1)
		{
			return false;
		}

		var method = methodAndPassword[..colonIndex].Trim();
		var password = methodAndPassword[(colonIndex + 1)..];
		if (method.Length == 0 || password.Length == 0)
		{
			return false;
		}

		if (!TryParseHostAndPort(hostAndPort, out var host, out var port))
		{
			return false;
		}

		entry = new ServerEntry
		{
			Method = method,
			Password = password,
			Host = host,
			Port = port
		};

		return true;
	}

	private static bool TryParseHostAndPort(string hostAndPort, out string host, out int port)
	{
		host = string.Empty;
		port = 0;

		if (string.IsNullOrWhiteSpace(hostAndPort))
		{
			return false;
		}

		hostAndPort = hostAndPort.Trim();
		string portSegment;

		if (hostAndPort.StartsWith("["))
		{
			var closing = hostAndPort.IndexOf(']');
			if (closing <= 0 || closing == hostAndPort.Length - 1)
			{
				return false;
			}

			host = hostAndPort.Substring(1, closing - 1);
			if (closing + 1 >= hostAndPort.Length || hostAndPort[closing + 1] != ':')
			{
				return false;
			}

			portSegment = hostAndPort[(closing + 2)..];
		}
		else
		{
			var lastColon = hostAndPort.LastIndexOf(':');
			if (lastColon <= 0 || lastColon == hostAndPort.Length - 1)
			{
				return false;
			}

			host = hostAndPort[..lastColon];
			portSegment = hostAndPort[(lastColon + 1)..];
		}

		if (!int.TryParse(portSegment, out port) || port <= 0 || port > 65535)
		{
			return false;
		}

		host = host.Trim();
		return host.Length > 0;
	}

	private static string DecodeUriComponent(string value)
	{
		try
		{
			return Uri.UnescapeDataString(value.Replace('+', ' '));
		}
		catch
		{
			return value;
		}
	}
}
