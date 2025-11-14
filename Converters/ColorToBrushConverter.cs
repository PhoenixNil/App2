using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App2.Converters;

public class ColorToBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is Color color)
		{
			return new SolidColorBrush(color);
		}

		return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> throw new NotSupportedException();
}
