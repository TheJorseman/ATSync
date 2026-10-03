using ATSync.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ATSync.App.Views;

public partial class MainWindow : Window
{
    private string? _lastPublishUri;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, __) => HookViewModel();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void HookViewModel()
    {
        if (Vm is null) return;
        Vm.Mods.OnPublish += uri => _lastPublishUri = uri;
        Vm.Detect.OnCopyRequested += s =>
        {
            try { Clipboard?.SetTextAsync(s).GetAwaiter().GetResult(); } catch { }
            Vm.StatusBar = $"✓ PeerID copiado al portapapeles ({s.Length} chars)";
        };
        Vm.OnShowAbout += s =>
        {
            // Diálogo simple vía MessageBox no estándar en Avalonia. Mostramos en status bar.
            Vm.StatusBar = s.Replace("\n", " — ");
        };
    }

    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vm?.Mods is { } mods)
            Dispatcher.UIThread.Post(() => mods.Filter(), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        Vm?.Mods.Filter();
    }

    private void OnModsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Vm?.Mods.UpdateSelection();
    }

    private void OnShowLastUri(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastPublishUri))
        {
            Vm.StatusBar = "Aún no has publicado un perfil en esta sesión.";
            return;
        }
        try { Clipboard?.SetTextAsync(_lastPublishUri).GetAwaiter().GetResult(); } catch { }
        Vm.StatusBar = $"✓ URI copiado al portapapeles: {_lastPublishUri}";
    }
}