using Microsoft.Maui.Controls;

namespace YouPander.Services;

public static class TemaService
{
    // Se asigna en MauiProgram antes del Build(), se consume en App.xaml.cs
    public static bool TemaPendiente { get; set; } = true;

    private static readonly Uri UriTemaOscuro =
        new("Resources/Styles/TemaOscuro.xaml", UriKind.Relative);
    private static readonly Uri UriTemaClaro =
        new("Resources/Styles/TemaClaro.xaml", UriKind.Relative);

    /// <summary>
    /// Llama desde App.xaml.cs (constructor) para aplicar el tema guardado.
    /// </summary>
    public static void AplicarTemaPendiente() => AplicarTema(TemaPendiente);

    /// <summary>
    /// Llama desde SettingsViewModel al cambiar el Switch en tiempo real.
    /// </summary>
    public static void AplicarTema(bool darkMode)
    {
        #region Version 1

        Uri uriNuevo = darkMode ? UriTemaOscuro : UriTemaClaro;
        var recursos = Application.Current!.Resources.MergedDictionaries;

        // Eliminar el tema de color actual si existe
        var actual = recursos.FirstOrDefault(d =>
            d.Source == UriTemaOscuro || d.Source == UriTemaClaro);

        if (actual != null)
        {
            recursos.Remove(actual);
        }

        // Añadir el nuevo — Add es suficiente porque los estilos usan DynamicResource,
        // que busca la clave en todos los diccionarios fusionados sin importar el orden
        var nuevoTema = darkMode ? CrearTemaOscuro() : CrearTemaClaro();
        recursos.Add(nuevoTema);

        #endregion

    }

    private static ResourceDictionary CrearTemaOscuro() => new ResourceDictionary
    {
        { "ColorTabSeleccionada", Color.FromArgb("#343A46") },
        { "ColorTabNoSeleccionada", Color.FromArgb("#242831") },
        { "ColorTextoTabSeleccionada", Color.FromArgb("#FFFFFF") },
        { "ColorTextoTabNoSeleccionada", Color.FromArgb("#B8BEC9") },

        { "ColorFondoPrincipal",    Color.FromArgb("#0F0F11") },
        { "ColorFondoSuperficie",   Color.FromArgb("#1A1A1F") },
        { "ColorFondoElevado",      Color.FromArgb("#24242B") },
        { "ColorFondoControl",      Color.FromArgb("#2E2E38") },

        { "ColorAcento",            Color.FromArgb("#7C6FF7") },
        { "ColorAcentoOscuro",      Color.FromArgb("#5A53D4") },
        { "ColorAcentoSuave",       Color.FromArgb("#3D3A6A") },
        { "ColorAcentoMuysuave",    Color.FromArgb("#25244A") },

        { "ColorTextoPrimario",     Color.FromArgb("#F0EFF8") },
        { "ColorTextoSecundario",   Color.FromArgb("#9998B0") },
        { "ColorTextoDeshabilitado",Color.FromArgb("#55545F") },
        { "ColorTextoAcento",       Color.FromArgb("#9F99FA") },

        { "ColorBordeSutil",        Color.FromArgb("#2A2A35") },
        { "ColorBordeNormal",       Color.FromArgb("#3A3A48") },

        { "ColorExito",             Color.FromArgb("#3DB87A") },
        { "ColorError",             Color.FromArgb("#E05555") },
        { "ColorAdvertencia",       Color.FromArgb("#D9913A") },
        { "ColorInfo",              Color.FromArgb("#4A9EE0") },
    };

    private static ResourceDictionary CrearTemaClaro() => new ResourceDictionary
    {
        { "ColorTabSeleccionada", Color.FromArgb("#FFFFFF") },
        { "ColorTabNoSeleccionada", Color.FromArgb("#EAECEF") },
        { "ColorTextoTabSeleccionada", Color.FromArgb("#252525") },
        { "ColorTextoTabNoSeleccionada", Color.FromArgb("#666666") },

        // Fondos
        { "ColorFondoPrincipal",     Color.FromArgb("#F5F3EF") },
        { "ColorFondoSuperficie",    Color.FromArgb("#FFFFFF") },
        { "ColorFondoElevado",       Color.FromArgb("#FAF9F6") },
        { "ColorFondoControl",       Color.FromArgb("#EEECE7") },

        // Acentos morados
        { "ColorAcento",             Color.FromArgb("#6558B5") },
        { "ColorAcentoOscuro",       Color.FromArgb("#50458F") },
        { "ColorAcentoSuave",        Color.FromArgb("#EAE7F5") },
        { "ColorAcentoMuysuave",     Color.FromArgb("#F3F1FA") },

        // Textos
        { "ColorTextoPrimario",      Color.FromArgb("#292824") },
        { "ColorTextoSecundario",    Color.FromArgb("#77746D") },
        { "ColorTextoDeshabilitado", Color.FromArgb("#AAA79F") },
        { "ColorTextoAcento",        Color.FromArgb("#574B9D") },

        // Bordes
        { "ColorBordeSutil",         Color.FromArgb("#E8E5DE") },
        { "ColorBordeNormal",        Color.FromArgb("#D6D2C9") },

        // Estados
        { "ColorExito",              Color.FromArgb("#347A50") },
        { "ColorError",              Color.FromArgb("#B84C4C") },
        { "ColorAdvertencia",        Color.FromArgb("#A66D24") },
        { "ColorInfo",               Color.FromArgb("#39769D") },
    };
}