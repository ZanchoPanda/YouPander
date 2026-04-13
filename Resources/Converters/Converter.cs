using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YouPander.Resources.Converters
{
    public class InverseBoolConverter : IValueConverter
    {
        // Convierte bool → bool invertido
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true; // valor por defecto
        }

        // Convierte de vuelta (si es necesario)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }
    }
}
