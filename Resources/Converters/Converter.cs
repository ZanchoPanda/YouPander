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


    // true → "⏸"  /  false → "▶"
    public class PlayPauseIconConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
            => value is true ? "⏸" : "▶";
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // IsVideo → "🎬" / false → "🎵"
    public class MediaIconConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
            => value is true ? "🎬" : "🎵";
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // Pestaña activa → color destacado
    public class TabColorConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
        {
            int selected = value is int i ? i : 0;
            int tab = p is string s && int.TryParse(s, out int parsed) ? parsed : 0;
            return selected == tab
                ? Color.FromArgb("#2196F3")
                : Colors.Transparent;
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // Misma lógica pero para FontAttributes
    public class TabFontConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
        {
            int selected = value is int i ? i : 0;
            int tab = p is string s && int.TryParse(s, out int parsed) ? parsed : 0;
            return selected == tab ? FontAttributes.Bold : FontAttributes.None;
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // Visibilidad de contenido de pestaña
    public class TabVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
        {
            int selected = value is int i ? i : 0;
            int tab = p is string s && int.TryParse(s, out int parsed) ? parsed : 0;
            return selected == tab;
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // null → false (oculta el panel si no hay nada reproduciendo)
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
            => value is not null;
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // Shuffle/Repeat activo → azul / inactivo → gris
    public class ActiveColorConverter : IValueConverter
    {
        public object Convert(object? value, Type t, object? p, CultureInfo c)
            => value is true ? Color.FromArgb("#2196F3") : Color.FromArgb("#888888");
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
            => throw new NotImplementedException();
    }


}
