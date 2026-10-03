using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace ATSync.App.Services;

/// <summary>
/// Acceso al portapapeles de la app desde VMs sin inyectar Window.
/// </summary>
public static class ClipboardService
{
    public static IClipboard? TryGet()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            && d.MainWindow is { } w)
            return w.Clipboard;
        return null;
    }

    public static void SetText(string text)
    {
        try { TryGet()?.SetTextAsync(text ?? "").GetAwaiter().GetResult(); } catch { }
    }

    public static string GetText()
    {
        try { return TryGet()?.GetTextAsync().GetAwaiter().GetResult() ?? ""; } catch { return ""; }
    }
}