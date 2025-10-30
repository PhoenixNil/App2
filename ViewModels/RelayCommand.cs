using System;
using System.Windows.Input;

namespace App2.ViewModels;

/// <summary>
/// RelayCommand 实现 ICommand 接口，用于在 ViewModel 中绑定命令
/// </summary>
public class RelayCommand : ICommand
{
	private readonly Action _execute;
	private readonly Func<bool>? _canExecute;

	public event EventHandler? CanExecuteChanged;

	public RelayCommand(Action execute, Func<bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

	public void Execute(object? parameter) => _execute();

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}

/// <summary>
/// 泛型版本的 RelayCommand，支持命令参数
/// </summary>
public class RelayCommand<T> : ICommand
{
	private readonly Action<T?> _execute;
	private readonly Func<T?, bool>? _canExecute;

	public event EventHandler? CanExecuteChanged;

	public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter)
	{
		return _canExecute?.Invoke((T?)parameter) ?? true;
	}

	public void Execute(object? parameter)
	{
		_execute((T?)parameter);
	}

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}

