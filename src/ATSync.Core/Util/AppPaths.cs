namespace ATSync.Core.Util;

/// <summary>
/// Rutas estándar donde ATSync guarda su estado. Todas se crean perezosamente.
/// </summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string ProfilesDir => Path.Combine(Root, "profiles");
    public string BlocksDir => Path.Combine(Root, "blocks");
    public string StagingDir => Path.Combine(Root, "staging");
    public string SessionsDir => Path.Combine(Root, "sessions");
    public string LogsDir => Path.Combine(Root, "logs");
    public string IdentityPath => Path.Combine(Root, "identity.dat");
    public string DhtCachePath => Path.Combine(Root, "dht.dat");
    public string SettingsPath => Path.Combine(Root, "settings.json");

    public AppPaths(string? root = null)
    {
        Root = root ?? DefaultRoot();
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(BlocksDir);
        Directory.CreateDirectory(StagingDir);
        Directory.CreateDirectory(SessionsDir);
        Directory.CreateDirectory(LogsDir);
    }

    public static string DefaultRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "ATSync");
    }

    /// <summary>Ruta canónica de mods del juego (configurable por el usuario).</summary>
    public static string DefaultAtsModsDir()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "American Truck Simulator", "mod");
    }

    public static string DefaultAtsHomeDir()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "American Truck Simulator");
    }
}