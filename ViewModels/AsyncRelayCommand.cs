using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace App2.ViewModels;

/// <summary>
/// 异步命令实现，用于处理异步操作
/// </summary>
public class AsyncRelayCommand : ICommand
{
	private readonly Func<Task> _execute;
	private readonly Func<bool>? _canExecute;
	private bool _isExecuting;

	public event EventHandler? CanExecuteChanged;

	public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter)
	{
		return !_isExecuting && (_canExecute?.Invoke() ?? true);
	}

	public async void Execute(object? parameter)
	{
		await ExecuteAsync();
	}

	/// <summary>
	/// 执行异步命令并返回 Task，允许调用者等待命令完成
	/// </summary>
	public async Task ExecuteAsync()
	{
		if (!CanExecute(null))
		{
			return;
		}

		_isExecuting = true;
		RaiseCanExecuteChanged();

		try
		{
			await _execute();
		}
		finally
		{
			_isExecuting = false;
			RaiseCanExecuteChanged();
		}
	}

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}

