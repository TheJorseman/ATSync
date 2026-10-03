using ATSync.App.Services;
using ATSync.App.ViewModels;
using ATSync.App.Views;
using ATSync.Core.Ats;
using ATSync.Core.Mods;
using ATSync.Core.P2P;
using ATSync.Core.Profiles;
using ATSync.Core.Util;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;

namespace ATSync.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Composición manual de servicios (DI ligera, sin Microsoft.Extensions.Hosting).
        Services = new AppServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(Services)
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Composición raíz de servicios de la app. Sustituye a un contenedor DI completo.
/// Carga perezosamente para evitar trabajo innecesario en arranque.
/// </summary>
public sealed class AppServices
{
    public AppPaths Paths { get; }
    public PeerIdentity Identity { get; }
    public ProfileRepository Profiles { get; }
    public ModScanner ModScanner { get; }
    public GameVersionDetector GameVer { get; }
    public DlcDetector DlcDet { get; }
    public ProfileManager AtsProfiles { get; }
    public ProfileTransfer Transfer { get; }
    public SettingsService Settings { get; }
    public ILogger Log { get; }

    /// <summary>Host libp2p v0.2 (Relay v2 + DCUtR ready). Creado perezosamente.</summary>
    private Libp2pHost? _libp2p;
    public Libp2pHost GetOrCreateLibp2pHost()
    {
        if (_libp2p is null)
        {
            _libp2p = new Libp2pHost(Identity, Log);
            _libp2p.AppProtocol.ProfilesDir = Paths.ProfilesDir;
            _libp2p.AppProtocol.ModsDir = Paths.StagingDir;
        }
        return _libp2p;
    }

    public AppServices(string? rootDir = null)
    {
        Paths = new AppPaths(rootDir);
        Identity = PeerIdentity.Load(Paths.IdentityPath);
        Profiles = new ProfileRepository(Paths.ProfilesDir);
        ModScanner = new ModScanner();
        GameVer = new GameVersionDetector();
        DlcDet = new DlcDetector();
        AtsProfiles = new ProfileManager();
        Transfer = new ProfileTransfer(Identity, Profiles);
        Transfer.ModSearchDirs.Add(Paths.StagingDir);
        Settings = new SettingsService(Paths.SettingsPath);

        Log = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .CreateLogger("ATSync.App");
    }
}