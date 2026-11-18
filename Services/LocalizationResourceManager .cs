using System.ComponentModel;
using System.Globalization;

namespace YouPander.Services
{
    public class LocalizationResourceManager : INotifyPropertyChanged
    {
        public static LocalizationResourceManager Instance { get; } = new();

        private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en");

        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key]
        {
            get
            {
                return Resources.Localization.Strings.ResourceManager
                    .GetString(key, _currentCulture) ?? $"[{key}]";
            }
        }

        public void SetCulture(string cultureCode)
        {
            var culture = CultureInfo.GetCultureInfo(cultureCode);

            // Guardar la cultura activa
            _currentCulture = culture;

            // Establecer la cultura predeterminada
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Establecer la cultura del hilo actual
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            // Actualizar todos los bindings
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs("Item[]")
                );

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(null)
                );
            });
        }

        public void DebugCulture()
        {
            System.Diagnostics.Debug.WriteLine($"Cultura del gestor: {_currentCulture.Name}");

            System.Diagnostics.Debug.WriteLine($"Cultura actual: {CultureInfo.CurrentUICulture.Name}");

            System.Diagnostics.Debug.WriteLine($"Recurso History: {this["History"]}");
        }

    }
}
