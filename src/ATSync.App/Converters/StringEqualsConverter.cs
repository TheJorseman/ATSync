using System.Globalization;
using Avalonia.Data.Converters;

namespace ATSync.App.Converters;

/// <summary>
/// Convierte un string a bool según `parameter` (true si value == parameter).
/// Uso XAML: IsChecked="{Binding Settings.Transport, Converter={StaticResource StringEqualsConverter}, ConverterParameter=tcp}"
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public static StringEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is true) ? parameter?.ToString() : Avalonia.AvaloniaProperty.UnsetValue;
}