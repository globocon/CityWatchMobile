using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace C4iSytemsMobApp.Helpers
{
    /// <summary>
    /// [Roster Module] - Converts a boolean expanded state to an arrow icon.
    /// </summary>
    public class BooleanToArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // \uf078 is angle-down, \uf077 is angle-up in FontAwesome
            return (bool)value ? "\uf077" : "\uf078";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
