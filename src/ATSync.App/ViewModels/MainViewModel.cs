using ATSync.App.Services;
using ATSync.Core.Ats;
using ATSync.Core.Mods;
using ATSync.Core.Models;
using ATSync.Core.P2P;
using ATSync.Core.Profiles;

namespace ATSync.App.ViewModels;

/// <summary>VM raíz — pestañas (Detect, Mods, Profiles, Import, Listen, Settings).</summary>
public sealed class MainViewModel : ObservableObject
{
    public string Title { get; } = "ATSync — ATS mod sync";

    // VM hijos
    public DetectViewModel Detect { get; }
    public ModsViewModel Mods { get; }
    public ProfilesViewModel Profiles { get; }
    public ImportViewModel Import { get; }
    public ListenViewModel Listen { get; }
    public SettingsViewModel Settings { get; }

    // Status bar
    private string _statusBar = "Listo";
    public string StatusBar { get => _statusBar; set => SetField(ref _statusBar, value); }

    public MainViewModel(AppServices s)
    {
        Detect  = new DetectViewModel(s);
        Mods    = new ModsViewModel(s);
        Profiles = new ProfilesViewModel(s);
        Import  = new ImportViewModel(s, Profiles, Mods, Detect, () => StatusBar);
        Listen  = new ListenViewModel(s);
        Settings = new SettingsViewModel(s);
    }
}

/// <summary>Pestaña 1 — Detect.</summary>
public sealed class DetectViewModel : ObservableObject
{
    private readonly AppServices _s;

    public string SteamPath { get; private set; } = "—";
    public string AtsInstall { get; private set; } = "—";
    public string AtsVersion { get; private set; } = "—";
    public string DlcsOwned { get; private set; } = "—";
    public string PeerId { get; private set; } = "—";

    public RelayCommand RefreshCommand { get; }

    public DetectViewModel(AppServices s)
    {
        _s = s;
        RefreshCommand = new RelayCommand(_ => Refresh());
        Refresh();
    }

    public void Refresh()
    {
        SteamPath = SteamPathLocator.FindSteamInstallPath() ?? "(no encontrado)";
        AtsInstall = SteamPathLocator.FindAtsInstallDir() ?? "(no encontrado)";
        var ver = _s.GameVer.DetectFromGameLog();
        AtsVersion = ver?.ToString() ?? "(no detectado — ejecuta ATS)";
        var dlcs = _s.DlcDet.Detect();
        DlcsOwned = $"{dlcs.Count(d => d.Owned)} / {dlcs.Count}";
        PeerId = _s.Identity.PeerId;
        OnPropertyChanged(nameof(SteamPath));
        OnPropertyChanged(nameof(AtsInstall));
        OnPropertyChanged(nameof(AtsVersion));
        OnPropertyChanged(nameof(DlcsOwned));
        OnPropertyChanged(nameof(PeerId));
    }
}

/// <summary>Pestaña 2 — Mods.</summary>
public sealed class ModsViewModel : ObservableObject
{
    private readonly AppServices _s;
    public System.Collections.ObjectModel.ObservableCollection<ModEntry> Items { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<ModEntry> Selected { get; } = new();

    public string ModsDir => _s.ModScanner.ModsDir;

    public RelayCommand RescanCommand { get; }
    public RelayCommand PublishCommand { get; }

    public event Action<string>? OnPublish;

    public ModsViewModel(AppServices s)
    {
        _s = s;
        RescanCommand = new RelayCommand(_ => Rescan());
        PublishCommand = new RelayCommand(_ => Publish(), _ => Selected.Count > 0);
        Rescan();
    }

    public void Rescan()
    {
        Items.Clear();
        Selected.Clear();
        foreach (var m in _s.ModScanner.Scan()) Items.Add(m);
        PublishCommand.RaiseCanExecuteChanged();
    }

    private void Publish()
    {
        if (Selected.Count == 0) return;
        var ver = _s.GameVer.DetectFromGameLog() ?? new GameVersion(1, 61, 0, 0, true);
        var ownedDlcs = _s.DlcDet.Detect().Where(d => d.Owned).Select(d => d.Id).ToList();
        var profile = ProfileBuilder.Build($"profile-{DateTime.Now:yyyyMMdd-HHmmss}",
            Selected.ToList(), _s.Identity, ver, ownedDlcs);
        var pub = new ProfilePublisher(_s.Identity, _s.Transfer, _s.Profiles, _s.Paths.StagingDir);
        var uri = pub.Publish(profile);
        OnPublish?.Invoke($"Publicado: {uri}");
    }
}

/// <summary>Pestaña 3 — Profiles (perfiles ATSync guardados).</summary>
public sealed class ProfilesViewModel : ObservableObject
{
    private readonly AppServices _s;
    public System.Collections.ObjectModel.ObservableCollection<AtsyncProfile> Items { get; } = new();
    public string ActivateStatus { get; private set; } = "";

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public string Filter { get; set; } = "";

    public ProfilesViewModel(AppServices s)
    {
        _s = s;
        RefreshCommand = new RelayCommand(_ => Refresh());
        ActivateCommand = new RelayCommand(_ => Activate());
        Refresh();
    }

    public void Refresh()
    {
        Items.Clear();
        foreach (var p in _s.Profiles.ListProfiles()) Items.Add(p);
    }

    public void SetActivateStatus(string s) { ActivateStatus = s; OnPropertyChanged(nameof(ActivateStatus)); }

    private void Activate()
    {
        // No-op: la activación real se hace desde la pestaña Import (opt-in).
        SetActivateStatus("Usa la pestaña 'Import' para activar un perfil recién importado.");
    }
}

/// <summary>Pestaña 4 — Import.</summary>
public sealed class ImportViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly ProfilesViewModel _profiles;
    private readonly ModsViewModel _mods;
    private readonly DetectViewModel _detect;
    private readonly Func<string> _statusBarGetter;

    public string Uri { get; set; } = "";
    public string PeerAddress { get; set; } = "";
    public bool ActivateAfter { get; set; }
    public string Status { get; private set; } = "";

    public RelayCommand ImportCommand { get; }

    public ImportViewModel(AppServices s, ProfilesViewModel profiles, ModsViewModel mods, DetectViewModel detect, Func<string> statusBarGetter)
    {
        _s = s; _profiles = profiles; _mods = mods; _detect = detect;
        _statusBarGetter = statusBarGetter;
        ImportCommand = new RelayCommand(async _ => await ImportAsync(), _ => CanImport());
    }

    private bool CanImport() =>
        !string.IsNullOrWhiteSpace(Uri) && !string.IsNullOrWhiteSpace(PeerAddress);

    public async Task ImportAsync()
    {
        try
        {
            if (!_s.Transfer.IsRunning) Start();
            var gameVer = _s.GameVer;
            var dlcDet = _s.DlcDet;
            var importer = new ProfileImporter(_s.Identity, _s.Transfer, _s.Profiles, gameVer, dlcDet, _s.Paths)
            {
                ValidateAgainstLocal = true
            };
            Status = "Importando…";
            OnPropertyChanged(nameof(Status));
            var result = await importer.ImportAsync(Uri, PeerAddress);
            if (result.Success)
            {
                Status = $"✓ Importado: {result.Profile?.Name} ({result.Mods.Count(m => m.Ok)}/{result.Mods.Count} mods OK)";
                _profiles.Refresh();
                _mods.Rescan();
                _detect.Refresh();
                if (ActivateAfter && result.Profile is not null)
                {
                    var firstAtsProfile = _s.AtsProfiles.ListProfiles().FirstOrDefault();
                    if (firstAtsProfile is not null)
                        _s.AtsProfiles.WriteActiveMods(firstAtsProfile.Id,
                            result.Profile.Mods.Select(m => m.Filename).Where(f => File.Exists(Path.Combine(Core.Util.AppPaths.DefaultAtsModsDir(), f))));
                }
            }
            else
            {
                Status = $"✗ {result.Error ?? "fallo"}";
            }
            OnPropertyChanged(nameof(Status));
        }
        catch (Exception ex) { Status = $"✗ {ex.Message}"; OnPropertyChanged(nameof(Status)); }
    }

    private void Start()
    {
        _s.Transfer.StartAsync(0).GetAwaiter().GetResult();
        _listenAddr = _s.Transfer.LocalAddress;
        OnPropertyChanged(nameof(ListenAddress));
    }
    private string _listenAddr = "";

    public string ListenAddress => _listenAddr;
}

/// <summary>Pestaña 5 — Listen (deja a la escucha).</summary>
public sealed class ListenViewModel : ObservableObject
{
    private readonly AppServices _s;
    public string ListenAddress { get; private set; } = "(sin iniciar)";
    public string Status { get; private set; } = "Detenido";

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public ListenViewModel(AppServices s)
    {
        _s = s;
        StartCommand = new RelayCommand(_ => Start(), _ => !_s.Transfer.IsRunning);
        StopCommand = new RelayCommand(_ => Stop(), _ => _s.Transfer.IsRunning);
        UpdateState();
    }

    private void UpdateState()
    {
        ListenAddress = _s.Transfer.IsRunning ? _s.Transfer.LocalAddress : "(sin iniciar)";
        Status = _s.Transfer.IsRunning ? "Escuchando" : "Detenido";
        OnPropertyChanged(nameof(ListenAddress));
        OnPropertyChanged(nameof(Status));
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private void Start()
    {
        try
        {
            _s.Transfer.StartAsync(_s.Settings.Data.ListenPort).GetAwaiter().GetResult();
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        UpdateState();
    }
    private void Stop()
    {
        try { _s.Transfer.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { /* ignore */ }
        UpdateState();
    }
}

/// <summary>Pestaña 6 — Settings.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _s;

    public bool ActivateOnImport
    {
        get => _s.Settings.Data.ActivateOnImport;
        set { _s.Settings.Data.ActivateOnImport = value; _s.Settings.Save(); OnPropertyChanged(); }
    }
    public bool PrivateProfilesOnly
    {
        get => _s.Settings.Data.PrivateProfilesOnly;
        set { _s.Settings.Data.PrivateProfilesOnly = value; _s.Settings.Save(); OnPropertyChanged(); }
    }
    public int ListenPort
    {
        get => _s.Settings.Data.ListenPort;
        set { _s.Settings.Data.ListenPort = value; _s.Settings.Save(); OnPropertyChanged(); }
    }

    public SettingsViewModel(AppServices s) { _s = s; }
}