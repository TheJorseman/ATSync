using ATSync.Core.Models;

namespace ATSync.Core.Ats;

/// <summary>
/// Lee los appmanifest_*.acf de Steam para detectar qué DLCs están instalados.
/// </summary>
public sealed class DlcDetector
{
    public string? SteamAppsDir { get; }

    public DlcDetector(string? steamAppsDir = null)
    {
        SteamAppsDir = steamAppsDir ?? SteamPathLocator.FindSteamAppsDir();
    }

    /// <summary>Devuelve la lista del catálogo con `Owned` actualizado según Steam.</summary>
    public IReadOnlyList<DlcInfo> Detect()
    {
        var list = DlcCatalog.All.Select(d => new DlcInfo
        {
            Id = d.Id,
            Name = d.Name,
            SteamAppId = d.SteamAppId,
            ManifestName = d.ManifestName,
            Category = d.Category,
            Owned = false
        }).ToList();

        if (SteamAppsDir is null || !Directory.Exists(SteamAppsDir))
            return list;

        foreach (var appId in DlcCatalog.All.Select(d => d.SteamAppId))
        {
            var manifest = Path.Combine(SteamAppsDir, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest)) continue;
            var name = ParseAcfName(manifest);
            foreach (var d in list)
            {
                if (d.SteamAppId == appId && (name == null || name.Contains(d.ManifestName, StringComparison.OrdinalIgnoreCase)))
                {
                    ((List<DlcInfo>)list).Remove(d);
                    list.Add(new DlcInfo
                    {
                        Id = d.Id, Name = d.Name, SteamAppId = d.SteamAppId,
                        ManifestName = d.ManifestName, Category = d.Category, Owned = true
                    });
                    break;
                }
            }
        }
        return list;
    }

    private static string? ParseAcfName(string acf)
    {
        try
        {
            foreach (var line in File.ReadLines(acf))
            {
                var t = line.Trim();
                if (t.StartsWith("\"name\""))
                {
                    var ix1 = t.IndexOf('"', 6);
                    var ix2 = t.IndexOf('"', ix1 + 1);
                    if (ix1 > 0 && ix2 > ix1) return t.Substring(ix1 + 1, ix2 - ix1 - 1);
                }
            }
        }
        catch { }
        return null;
    }
}