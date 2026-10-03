using ATSync.Core.Models;
using ATSync.Core.Util;

namespace ATSync.Core.Mods;

/// <summary>Escanea la carpeta de mods del juego, parsea cada manifest.sii y construye un ModEntry.</summary>
public sealed class ModScanner
{
    public string ModsDir { get; }
    public ModScanner(string? modsDir = null)
    {
        ModsDir = modsDir ?? Util.AppPaths.DefaultAtsModsDir();
    }

    public IEnumerable<ModEntry> Scan()
    {
        if (!Directory.Exists(ModsDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(ModsDir, "*.scs", SearchOption.TopDirectoryOnly))
            yield return ScanFile(file);

        foreach (var file in Directory.EnumerateFiles(ModsDir, "*.zip", SearchOption.TopDirectoryOnly))
            yield return ScanFile(file);
    }

    public ModEntry ScanFile(string path)
    {
        var info = new FileInfo(path);
        var sha = SafeComputeHash(path);

        var entry = new ModEntry
        {
            Filename = Path.GetFileName(path),
            FullPath = path,
            Size = info.Length,
            Sha256Hex = sha,
            DisplayName = Path.GetFileNameWithoutExtension(path),
            PackageVersion = "0.0",
            CompatibleVersions = new[] { "*" },
            DlcDependencies = Array.Empty<string>()
        };

        try
        {
            using var scs = new ScsArchive(path);
            if (scs.HasEntry("manifest.sii"))
            {
                var text = scs.ReadAllText("manifest.sii");
                if (text is not null)
                {
                    var parsed = SiiManifestParser.Parse(text);
                    entry = entry with
                    {
                        DisplayName = string.IsNullOrEmpty(parsed.DisplayName) ? entry.DisplayName : parsed.DisplayName,
                        Author = parsed.Author,
                        PackageVersion = parsed.PackageVersion,
                        DescriptionFile = parsed.DescriptionFile,
                        IconPath = parsed.Icon,
                        Categories = parsed.Categories,
                        CompatibleVersions = parsed.CompatibleVersions.Count == 0 ? entry.CompatibleVersions : parsed.CompatibleVersions,
                        DlcDependencies = parsed.DlcDependencies,
                        MpModOptional = parsed.MpModOptional
                    };
                }
            }
        }
        catch
        {
            // zip inválido o sin manifest — devolvemos entrada básica
        }
        return entry;
    }

    private static string SafeComputeHash(string path)
    {
        try { return Hashing.Sha256HexOfFile(path); }
        catch { return ""; }
    }
}