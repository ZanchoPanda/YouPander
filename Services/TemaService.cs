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
    }

    private static ResourceDictionary CrearTemaOscuro() => new ResourceDictionary
    {
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
        { "ColorFondoPrincipal",    Color.FromArgb("#FDF1DD") },
        { "ColorFondoSuperficie",   Color.FromArgb("#FDE1DD") },
        { "ColorFondoElevado",      Color.FromArgb("#FDDDE9") },
        { "ColorFondoControl",      Color.FromArgb("#E8E6E0") },

        { "ColorAcento",            Color.FromArgb("#DDE9FD") },
        { "ColorAcentoOscuro",      Color.FromArgb("#4F45C8") },
        { "ColorAcentoSuave",       Color.FromArgb("#F1DDFD") },
        { "ColorAcentoMuysuave",    Color.FromArgb("#EEECFe") },

        { "ColorTextoPrimario",     Color.FromArgb("#1A1A22") },
        { "ColorTextoSecundario",   Color.FromArgb("#6B6A7E") },
        { "ColorTextoDeshabilitado",Color.FromArgb("#B0AFBA") },
        { "ColorTextoAcento",       Color.FromArgb("#5047D4") },

        { "ColorBordeSutil",        Color.FromArgb("#DDD9D2") },
        { "ColorBordeNormal",       Color.FromArgb("#C8C4BC") },

        { "ColorExito",             Color.FromArgb("#2A9B63") },
        { "ColorError",             Color.FromArgb("#CC3333") },
        { "ColorAdvertencia",       Color.FromArgb("#C07A1A") },
        { "ColorInfo",              Color.FromArgb("#2A7DC0") },
    };
}