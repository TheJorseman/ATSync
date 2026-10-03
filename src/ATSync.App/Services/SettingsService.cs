using System.IO;
using System.Text.Json;

namespace ATSync.App.Services;

/// <summary>Configuración persistente mínima (JSON en %LOCALAPPDATA%\ATSync\settings.json).</summary>
public sealed class SettingsService
{
    private readonly string _path;
    public Settings Data { get; private set; }

    public SettingsService(string path)
    {
        _path = path;
        Data = Load();
    }

    public Settings Load()
    {
        if (!File.Exists(_path))
            return new Settings();
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* ignore */ }
    }
}

public sealed class Settings
{
    /// <summary>Si true, reescribe mod.sii con backup al activar un perfil ATSync.</summary>
    public bool ActivateOnImport { get; set; }

    /// <summary>Auto-relay (placeholder, v0.3).</summary>
    public bool AutoRelay { get; set; } = true;

    /// <summary>Puerto preferido para ProfileTransfer (0 = aleatorio).</summary>
    public int ListenPort { get; set; }

    /// <summary>Si true, sólo acepta perfiles firmados con clave conocida (v0.4).</summary>
    public bool PrivateProfilesOnly { get; set; }

    /// <summary>Transporte preferido: "tcp" (default) o "libp2p" (v0.2+).</summary>
    public string Transport { get; set; } = "tcp";

    /// <summary>Puerto libp2p preferido (0 = aleatorio, default 4001).</summary>
    public int Libp2pPort { get; set; } = 4001;

    /// <summary>Si true, habilita Circuit Relay v2 en libp2p (necesario para NAT traversal).</summary>
    public bool Libp2pEnableRelay { get; set; } = true;
}