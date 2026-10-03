using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Controls;

namespace ATSync.App.ViewModels;

/// <summary>
/// Convierte un nombre semántico de color ("ok" | "warn" | "err" | "info" | "muted" | "neutral")
/// en la clase correspondiente del chip.
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public static StatusColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString() ?? "";
        // Devuelve el nombre de clase que se añadirá al Border.
        // El XAML usa `Border.Classes` con un binding que ya provee la cadena exacta.
        return s;
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