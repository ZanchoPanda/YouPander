using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace YouPander.Services
{
    public class LocalizationResourceManager : INotifyPropertyChanged
    {
        public static LocalizationResourceManager Instance { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key]
        {
            get
            {
                return Resources.Localization.Strings.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture) ?? string.Empty;
            }
        }

        public void SetCulture(string cultureCode)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureCode);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
