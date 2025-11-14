using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace App2.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
		if (value is bool flag)
		{
			var visible = invert ? !flag : flag;
			return visible ? Visibility.Visible : Visibility.Collapsed;
		}

		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> throw new NotSupportedException();
}
