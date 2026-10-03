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

    public DetectViewModel Detect { get; }
    public ModsViewModel Mods { get; }
    public ProfilesViewModel Profiles { get; }
    public ImportViewModel Import { get; }
    public ListenViewModel Listen { get; }
    public SettingsViewModel Settings { get; }

    private string _statusBar = "Listo — abre la pestaña 1 para detectar tu instalación de ATS";
    public string StatusBar { get => _statusBar; set => SetField(ref _statusBar, value); }

    public RelayCommand ShowAboutCommand { get; }

    public event Action<string>? OnShowAbout;

    public MainViewModel(AppServices s)
    {
        Detect  = new DetectViewModel(s, () => StatusBar = $"Detect: {s.GameVer.DetectFromGameLog()?.ToString() ?? "—"}");
        Mods    = new ModsViewModel(s, msg => StatusBar = msg, () => StatusBar = $"{Detect.DlcsOwned} / 45 DLCs");
        Profiles = new ProfilesViewModel(s, msg => StatusBar = msg);
        Import  = new ImportViewModel(s, Profiles, Mods, Detect, m => StatusBar = m);
        Listen  = new ListenViewModel(s, msg => StatusBar = msg);
        Settings = new SettingsViewModel(s);

        ShowAboutCommand = new RelayCommand(_ => OnShowAbout?.Invoke("ATSync v0.1.0\n\nSincronizador P2P de mods para ATS.\nLicencia GPL-3.0.\n\nhttps://github.com/TheJorseman/ATSync"));

        // Eventos P2P
        s.Transfer.PeerConnected += (_, peerId) => StatusBar = $"✓ Peer conectado: {peerId[..Math.Min(20, peerId.Length)]}…";
        s.Transfer.ModReceived   += (_, e)      => StatusBar = $"✓ Mod recibido: {Path.GetFileName(e.LocalPath)} ({e.Size:N0} B)";
        s.Transfer.Error          += (_, msg)    => StatusBar = $"⚠ P2P error: {msg}";
    }
}

/// <summary>Pestaña 1 — Detect.</summary>
public sealed class DetectViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly Action? _onChange;

    public string SteamPath { get; private set; } = "—";
    public string AtsInstall { get; private set; } = "—";
    public string AtsVersion { get; private set; } = "—";
    public string DlcsOwned { get; private set; } = "—";
    public string PeerId { get; private set; } = "—";
    public string AtsVersionGlyph { get; private set; } = "⚪";

    public System.Collections.ObjectModel.ObservableCollection<DlcGroup> DlcGroups { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand CopyPeerIdCommand { get; }

    public event Action<string>? OnCopyRequested;

    public DetectViewModel(AppServices s, Action? onChange = null)
    {
        _s = s;
        _onChange = onChange;
        RefreshCommand = new RelayCommand(_ => Refresh());
        CopyPeerIdCommand = new RelayCommand(_ => OnCopyRequested?.Invoke(PeerId));
        Refresh();
    }

    public void Refresh()
    {
        SteamPath = SteamPathLocator.FindSteamInstallPath() ?? "(no encontrado)";
        AtsInstall = SteamPathLocator.FindAtsInstallDir() ?? "(no encontrado)";
        var ver = _s.GameVer.DetectFromGameLog();
        if (ver is not null) { AtsVersion = ver.ToString(); AtsVersionGlyph = "🟢"; }
        else                 { AtsVersion = "(no detectado — ejecuta ATS)"; AtsVersionGlyph = "🔴"; }

        var dlcs = _s.DlcDet.Detect();
        DlcsOwned = $"{dlcs.Count(d => d.Owned)} / {dlcs.Count}";
        PeerId = _s.Identity.PeerId;

        DlcGroups.Clear();
        foreach (var grp in dlcs.GroupBy(d => d.Category).OrderBy(g => g.Key))
        {
            DlcGroups.Add(new DlcGroup(
                CategoryLabel(grp.Key),
                grp.Key,
                new System.Collections.ObjectModel.ObservableCollection<DlcView>(
                    grp.OrderBy(d => d.Name).Select(d => new DlcView(d)))));
        }

        OnPropertyChanged(nameof(SteamPath));
        OnPropertyChanged(nameof(AtsInstall));
        OnPropertyChanged(nameof(AtsVersion));
        OnPropertyChanged(nameof(AtsVersionGlyph));
        OnPropertyChanged(nameof(DlcsOwned));
        OnPropertyChanged(nameof(PeerId));
        _onChange?.Invoke();
    }

    public static string CategoryLabel(string cat) => cat switch
    {
        "map" => "Mapas",
        "cargo" => "Carga",
        "tuning" => "Tuning",
        "truck" => "Camiones",
        "paint" => "Pintura",
        "road_trip" => "Road Trip",
        _ => cat,
    };
}

public sealed class DlcGroup
{
    public string Title { get; }
    public string Category { get; }
    public int OwnedCount { get; }
    public int TotalCount { get; }
    public System.Collections.ObjectModel.ObservableCollection<DlcView> Items { get; }
    public DlcGroup(string title, string category, System.Collections.ObjectModel.ObservableCollection<DlcView> items)
    {
        Title = title; Category = category; Items = items;
        OwnedCount = items.Count(i => i.Owned);
        TotalCount = items.Count;
    }
}

/// <summary>Pestaña 2 — Mods (con validación inline).</summary>
public sealed class ModsViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly Action<string>? _onStatus;
    private readonly Action? _onChange;

    public System.Collections.ObjectModel.ObservableCollection<ModView> Items { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<ModView> Selected { get; } = new();

    public string ModsDir => _s.ModScanner.ModsDir;
    public string FilterText { get; set; } = "";
    public string SelectionSummary { get; private set; } = "0 mods seleccionados · 0 B total";
    public string TotalSummary { get; private set; } = "0 mods escaneados";

    public RelayCommand RescanCommand { get; }
    public RelayCommand PublishCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }

    public event Action<string>? OnPublish;

    public ModsViewModel(AppServices s, Action<string>? onStatus = null, Action? onChange = null)
    {
        _s = s; _onStatus = onStatus; _onChange = onChange;
        RescanCommand = new RelayCommand(_ => Rescan());
        PublishCommand = new RelayCommand(_ => Publish(), _ => Selected.Count > 0);
        SelectAllCommand = new RelayCommand(_ => SelectAll());
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        Rescan();
    }

    public void Rescan()
    {
        Items.Clear();
        Selected.Clear();
        var installed = _s.GameVer.DetectFromGameLog();
        var owned = _s.DlcDet.Detect().Where(d => d.Owned).Select(d => d.Id).ToHashSet();
        foreach (var e in _s.ModScanner.Scan())
        {
            var v = new ModView(e, installed, owned);
            if (string.IsNullOrEmpty(FilterText) || ContainsFilter(v, FilterText))
                Items.Add(v);
        }
        UpdateSummaries();
        PublishCommand.RaiseCanExecuteChanged();
        _onChange?.Invoke();
    }

    public void Filter()
    {
        var installed = _s.GameVer.DetectFromGameLog();
        var owned = _s.DlcDet.Detect().Where(d => d.Owned).Select(d => d.Id).ToHashSet();
        var prevSelection = Selected.ToList();
        Items.Clear();
        Selected.Clear();
        foreach (var e in _s.ModScanner.Scan())
        {
            var v = new ModView(e, installed, owned);
            if (string.IsNullOrEmpty(FilterText) || ContainsFilter(v, FilterText))
            {
                Items.Add(v);
                if (prevSelection.Any(p => p.FileName == v.FileName)) Selected.Add(v);
            }
        }
        UpdateSummaries();
        PublishCommand.RaiseCanExecuteChanged();
    }

    private static bool ContainsFilter(ModView v, string q)
    {
        q = q.Trim();
        if (q.Length == 0) return true;
        return (v.FileName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (v.DisplayName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (v.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public void UpdateSelection()
    {
        UpdateSummaries();
        PublishCommand.RaiseCanExecuteChanged();
    }

    private void UpdateSummaries()
    {
        var totalBytes = Selected.Sum(m => m.Entry.Size);
        SelectionSummary = $"{Selected.Count} mods seleccionados · {FormatSize(totalBytes)}";
        TotalSummary = $"{Items.Count} mods escaneados";
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(TotalSummary));
    }

    public void SelectAll()
    {
        foreach (var v in Items)
            if (!Selected.Contains(v)) Selected.Add(v);
        UpdateSelection();
    }

    public void ClearSelection()
    {
        Selected.Clear();
        UpdateSelection();
    }

    private void Publish()
    {
        if (Selected.Count == 0) return;
        var ver = _s.GameVer.DetectFromGameLog() ?? new GameVersion(1, 61, 0, 0, true);
        var ownedDlcs = _s.DlcDet.Detect().Where(d => d.Owned).Select(d => d.Id).ToList();
        var profile = ProfileBuilder.Build($"profile-{DateTime.Now:yyyyMMdd-HHmmss}",
            Selected.Select(m => m.Entry).ToList(), _s.Identity, ver, ownedDlcs);
        var pub = new ProfilePublisher(_s.Identity, _s.Transfer, _s.Profiles, _s.Paths.StagingDir);
        var uri = pub.Publish(profile);
        _onStatus?.Invoke($"Publicado: {uri}");
        OnPublish?.Invoke(uri.ToString());
    }

    private static string FormatSize(long b)
    {
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:0.0} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):0.0} MB";
        return $"{b / (1024.0 * 1024 * 1024):0.0} GB";
    }
}

/// <summary>Pestaña 3 — Profiles (perfiles ATSync guardados).</summary>
public sealed class ProfilesViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly Action<string>? _onStatus;
    public System.Collections.ObjectModel.ObservableCollection<ProfileView> Items { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand DeleteCommand { get; }
    private ProfileView? _selectedProfile;
    public ProfileView? SelectedProfile { get => _selectedProfile; set => SetField(ref _selectedProfile, value); }

    public ProfilesViewModel(AppServices s, Action<string>? onStatus = null)
    {
        _s = s; _onStatus = onStatus;
        RefreshCommand = new RelayCommand(_ => Refresh());
        DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedProfile is not null);
        Refresh();
    }

    public void Refresh()
    {
        Items.Clear();
        foreach (var p in _s.Profiles.ListProfiles()) Items.Add(new ProfileView(p));
    }

    private void Delete()
    {
        if (SelectedProfile is null) return;
        var name = SelectedProfile.Name;
        // busca el archivo y borra
        var path = System.IO.Path.Combine(_s.Paths.ProfilesDir, MakeSafe(name) + ".atsync");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        Refresh();
        _onStatus?.Invoke($"Eliminado perfil '{name}'");
    }

    private static string MakeSafe(string s)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

/// <summary>Pestaña 4 — Import.</summary>
public sealed class ImportViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly ProfilesViewModel _profiles;
    private readonly ModsViewModel _mods;
    private readonly DetectViewModel _detect;
    private readonly Action<string> _onStatus;

    public string Uri { get; set; } = "";
    public string PeerAddress { get; set; } = "";
    public bool ActivateAfter { get; set; }
    public string Status { get; private set; } = "";
    public string StatusColor { get; private set; } = "muted";
    public string ProgressLabel { get; private set; } = "";

    public RelayCommand ImportCommand { get; }
    public RelayCommand CopyStatusCommand { get; }
    public RelayCommand PasteUriCommand { get; }

    public ImportViewModel(AppServices s, ProfilesViewModel profiles, ModsViewModel mods, DetectViewModel detect, Action<string> onStatus)
    {
        _s = s; _profiles = profiles; _mods = mods; _detect = detect; _onStatus = onStatus;
        ImportCommand = new RelayCommand(async _ => await ImportAsync(), _ => CanImport());
        CopyStatusCommand = new RelayCommand(_ => CopyToClipboard(Status));
        PasteUriCommand = new RelayCommand(_ => TryPasteFromClipboard());
        TryValidateUri();
    }

    private bool CanImport() =>
        !string.IsNullOrWhiteSpace(Uri) && !string.IsNullOrWhiteSpace(PeerAddress);

    private void TryValidateUri()
    {
        if (string.IsNullOrEmpty(Uri))
        {
            Status = ""; StatusColor = "muted";
        }
        else if (Uri.StartsWith("atsync://profile/", StringComparison.OrdinalIgnoreCase))
        {
            Status = $"✓ URI válido: profile con CID {Uri["atsync://profile/".Length..Math.Min(20, Uri.Length - "atsync://profile/".Length)]}…";
            StatusColor = "ok";
        }
        else if (Uri.StartsWith("atsync://mod/", StringComparison.OrdinalIgnoreCase))
        {
            Status = $"✓ URI válido: mod con CID {Uri["atsync://mod/".Length..Math.Min(20, Uri.Length - "atsync://mod/".Length)]}…";
            StatusColor = "ok";
        }
        else
        {
            Status = "✗ URI inválido. Debe empezar por atsync://profile/ o atsync://mod/";
            StatusColor = "err";
        }
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
    }

    private void TryPasteFromClipboard()
    {
        var text = ClipboardService.GetText();
        if (!string.IsNullOrEmpty(text))
        {
            Uri = text.Trim();
            TryValidateUri();
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            ClipboardService.SetText(text);
            _onStatus($"Copiado al portapapeles");
        }
        catch { _onStatus("No se pudo copiar"); }
    }

    public async Task ImportAsync()
    {
        try
        {
            if (!_s.Transfer.IsRunning) Start();
            var importer = new ProfileImporter(_s.Identity, _s.Transfer, _s.Profiles, _s.GameVer, _s.DlcDet, _s.Paths)
            {
                ValidateAgainstLocal = true
            };
            Status = "⏳ Conectando y descargando perfil…";
            StatusColor = "info";
            ProgressLabel = "Conectando…";
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ProgressLabel));

            var result = await importer.ImportAsync(Uri, PeerAddress);
            if (result.Success)
            {
                Status = $"✓ Importado: {result.Profile?.Name} ({result.Mods.Count(m => m.Ok)}/{result.Mods.Count} mods OK)";
                StatusColor = "ok";
                ProgressLabel = "Completo";
                _profiles.Refresh();
                _mods.Rescan();
                _detect.Refresh();
                if (ActivateAfter && result.Profile is not null)
                {
                    var firstAtsProfile = _s.AtsProfiles.ListProfiles().FirstOrDefault();
                    if (firstAtsProfile is not null)
                    {
                        var src = System.IO.Path.Combine(Core.Util.AppPaths.DefaultAtsModsDir());
                        var toActivate = result.Profile.Mods
                            .Select(m => m.Filename)
                            .Where(f => System.IO.File.Exists(System.IO.Path.Combine(src, f)))
                            .ToList();
                        _s.AtsProfiles.WriteActiveMods(firstAtsProfile.Id, toActivate);
                        _onStatus($"Activados {toActivate.Count} mods en perfil ATS '{firstAtsProfile.Id}' (backup creado)");
                    }
                }
            }
            else
            {
                Status = $"✗ {result.Error ?? "fallo"}";
                StatusColor = "err";
                ProgressLabel = "Error";
            }
        }
        catch (Exception ex)
        {
            Status = $"✗ {ex.Message}";
            StatusColor = "err";
            ProgressLabel = "Error";
        }
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(ProgressLabel));
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
    private readonly Action<string> _onStatus;
    public string ListenAddress { get; private set; } = "(sin iniciar)";
    public string Status { get; private set; } = "Detenido";
    public string StatusGlyph { get; private set; } = "⚪";
    public int ConnectedPeers { get; private set; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand CopyAddressCommand { get; }

    public ListenViewModel(AppServices s, Action<string> onStatus)
    {
        _s = s; _onStatus = onStatus;
        StartCommand = new RelayCommand(_ => Start(), _ => !_s.Transfer.IsRunning);
        StopCommand = new RelayCommand(_ => Stop(), _ => _s.Transfer.IsRunning);
        CopyAddressCommand = new RelayCommand(_ => CopyToClipboard(ListenAddress));

        _s.Transfer.PeerConnected += (_, peerId) =>
        {
            ConnectedPeers++;
            OnPropertyChanged(nameof(ConnectedPeers));
            onStatus($"Peer conectado: {peerId[..Math.Min(20, peerId.Length)]}…");
        };
        UpdateState();
    }

    private void UpdateState()
    {
        ListenAddress = _s.Transfer.IsRunning ? _s.Transfer.LocalAddress : "(sin iniciar)";
        Status = _s.Transfer.IsRunning ? "Escuchando" : "Detenido";
        StatusGlyph = _s.Transfer.IsRunning ? "🟢" : "⚪";
        OnPropertyChanged(nameof(ListenAddress));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusGlyph));
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private void Start()
    {
        try
        {
            _s.Transfer.StartAsync(_s.Settings.Data.ListenPort).GetAwaiter().GetResult();
            _onStatus($"Listening en {_s.Transfer.LocalAddress}");
        }
        catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        UpdateState();
    }
    private void Stop()
    {
        try { _s.Transfer.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        catch { /* ignore */ }
        UpdateState();
        _onStatus("Listening detenido");
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            ClipboardService.SetText(text);
            _onStatus($"Copiado al portapapeles");
        }
        catch { _onStatus("No se pudo copiar"); }
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