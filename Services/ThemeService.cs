using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Services
{
    public static class ThemeService
    {

        public static void ApplyTheme(string colorHex, bool darkMode)
        {
            var resources = Application.Current.Resources;

            // Color principal
            if (!string.IsNullOrEmpty(colorHex))
            {
                resources["PrimaryColor"] = Color.FromArgb(colorHex);
            }

            // Modo oscuro / claro
            if (darkMode)
            {
                resources["BackgroundColor"] = Colors.Black;
                resources["TextColor"] = Colors.White;
            }
            else
            {
                resources["BackgroundColor"] = Colors.White;
                resources["TextColor"] = Colors.Black;
            }
        }

    }
}
