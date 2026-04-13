using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using YouPander.Resources.Localization;

namespace YouPander.Services
{
    public static class LocalizationService
    {
        public static string Get(string key)
        {
            var value = Strings.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture);
            return value ?? string.Empty;
        }

        public static void SetLanguage(string languageCode)
        {
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }
}
