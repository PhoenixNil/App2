using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace App2.Models;

public class ServerEntry : INotifyPropertyChanged
{
	public string Name { get; set; } = string.Empty;
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; }
	public string Method { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;

	private bool _isActive;
	public bool IsActive
	{
		get => _isActive;
		set
		{
			if (_isActive != value)
			{
				_isActive = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsActiveVisibility));
			}
		}
	}

	public Visibility IsActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

	public event PropertyChangedEventHandler? PropertyChanged;

	public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public ServerEntry Clone()
	{
		return new ServerEntry
		{
			Name = Name,
			Host = Host,
			Port = Port,
			Password = Password,
			Method = Method,
			IsActive = IsActive
		};
	}
}

