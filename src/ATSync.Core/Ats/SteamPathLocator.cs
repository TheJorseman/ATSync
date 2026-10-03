using Microsoft.Win32;

namespace ATSync.Core.Ats;

/// <summary>
/// Localiza la instalación de Steam y de American Truck Simulator.
/// </summary>
public static class SteamPathLocator
{
    public const string AtsAppId = "270880";

    /// <summary>Devuelve la ruta de Steam (InstallPath) o null si no se encuentra.</summary>
    public static string? FindSteamInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (key is null) return null;
            return key.GetValue("InstallPath") as string;
        }
        catch { return null; }
    }

    /// <summary>Devuelve las rutas candidatas donde puede estar ATS, ordenadas por preferencia.</summary>
    public static IReadOnlyList<string> FindAtsCandidates()
    {
        var list = new List<string>();
        var steam = FindSteamInstallPath();
        if (steam is not null)
        {
            list.Add(Path.Combine(steam, "steamapps", "common", "American Truck Simulator"));
            list.Add(Path.Combine(steam, "steamapps", "common", "American Truck Simulator Demo"));
        }
        return list;
    }

    /// <summary>Devuelve la primera ruta que contenga el ejecutable amtrucks.exe o version.scs.</summary>
    public static string? FindAtsInstallDir()
    {
        foreach (var c in FindAtsCandidates())
        {
            if (!Directory.Exists(c)) continue;
            if (File.Exists(Path.Combine(c, "version.scs")) ||
                File.Exists(Path.Combine(c, "bin", "win_x64", "amtrucks.exe")))
                return c;
        }
        return null;
    }

    /// <summary>Devuelve la carpeta steamapps (donde están los appmanifest_*.acf).</summary>
    public static string? FindSteamAppsDir()
    {
        var steam = FindSteamInstallPath();
        return steam is null ? null : Path.Combine(steam, "steamapps");
    }
}