using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace App2.Services;

/// <summary>
/// 对话框与 UI 交互抽象。
/// </summary>
public interface IDialogService
{
	Task ShowMessageAsync(string title, string message, string closeButtonText = "确定");
	Task ShowErrorAsync(string title, string message);
	Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "确定", string cancelText = "取消", bool isDanger = false);
	Task<string?> PromptForTextAsync(string title, string placeholder, string? description = null, bool multiline = false);
	Task<ServerDialogResult?> ShowServerEditorAsync(
		string title,
		ServerDialogResult? defaults,
		IReadOnlyList<string> methodOptions,
		Func<ServerDialogResult, string?>? validate = null);
	Task<int?> ShowLocalPortDialogAsync(int currentPort, int minPort, int maxPort);
	Task ShowPortChangedReminderAsync();
	Task ShowLogsAsync(string logText, bool allowCopy);
}
