using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Controls;
using Avalonia.Media;

namespace ATSync.App.ViewModels;

/// <summary>
/// Convierte un nombre semántico de color ("ok" | "warn" | "err" | "info" | "muted" | "neutral")
/// en un <see cref="IBrush"/> listo para asignar a Background/Foreground.
///
/// Sustituye al patrón anterior `<Binding>` dentro de `<Border.Classes>` que se rompió
/// en Avalonia 11.2.x (InvalidCastException al cargar MainWindow).
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public static StatusColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString() ?? "";
        return s switch
        {
            "ok"   => new SolidColorBrush(Color.FromRgb(0x0F, 0x3A, 0x1E)),  // verde oscuro
            "warn" => new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x07)),  // amarillo oscuro
            "err"  => new SolidColorBrush(Color.FromRgb(0x3A, 0x0F, 0x0F)),  // rojo oscuro
            "info" => new SolidColorBrush(Color.FromRgb(0x0A, 0x2A, 0x35)),  // azul oscuro
            _      => new SolidColorBrush(Color.FromRgb(0x18, 0x22, 0x2D)),  // chip-muted por defecto
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Variante que mapea un nombre semántico a múltiples clases.
/// </summary>
public sealed class DlcStateConverter : IValueConverter
{
    public static DlcStateConverter Instance { get; } = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() ?? "";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}