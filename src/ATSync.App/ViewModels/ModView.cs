using ATSync.App.Services;
using ATSync.Core.Models;
using ATSync.Core.Mods;

namespace ATSync.App.ViewModels;

/// <summary>Wrapper de ModEntry con metadata derivada para la UI.</summary>
public sealed class ModView
{
    public ModEntry Entry { get; }
    public string FileName => Entry.Filename;
    public string DisplayName => string.IsNullOrEmpty(Entry.DisplayName) ? Entry.Filename : Entry.DisplayName;
    public string Author => string.IsNullOrEmpty(Entry.Author) ? "—" : Entry.Author;
    public string PackageVersion => string.IsNullOrEmpty(Entry.PackageVersion) ? "?" : Entry.PackageVersion;
    public string SizeText => FormatSize(Entry.Size);
    public string Cid => Entry.Sha256Hex.Length >= 12 ? Entry.Sha256Hex[..12] : Entry.Sha256Hex;

    public string VersionStatus { get; }
    public string DlcStatus { get; }
    public string StatusColor { get; }   // "ok" | "warn" | "err"
    public string StatusGlyph { get; }    // ● 🟢 🟡 🔴
    public string StatusTooltip { get; }

    public string CategoriesText { get; }
    public string CompatText { get; }

    public ModView(ModEntry e, GameVersion installed, IReadOnlyCollection<string> ownedDlcs)
    {
        Entry = e;
        if (e.Categories.Count > 0)
            CategoriesText = string.Join(", ", e.Categories);
        else
            CategoriesText = "—";
        if (e.CompatibleVersions.Count > 0)
            CompatText = string.Join(", ", e.CompatibleVersions);
        else
            CompatText = "* (sin restricción)";

        // Versión
        var versionOk = installed is null || e.CompatibleVersions.Count == 0 ||
                        e.CompatibleVersions.Any(v => installed.MatchesWildcard(v));
        VersionStatus = versionOk ? "✓" : "✗";

        // DLCs
        var missing = e.DlcDependencies.Where(d => !ownedDlcs.Contains(d)).ToList();
        if (e.DlcDependencies.Count == 0)
        {
            DlcStatus = "—";
        }
        else if (missing.Count == 0)
        {
            DlcStatus = "✓";
        } else
        {
            DlcStatus = $"Falta: {string.Join(",", missing)}";
        }

        if (!versionOk)
        {
            StatusColor = "err";
            StatusGlyph = "🔴";
            StatusTooltip = $"Versión incompatible: necesita {CompatText}, instalado {installed}";
        }
        else if (missing.Count > 0)
        {
            StatusColor = "warn";
            StatusGlyph = "🟡";
            StatusTooltip = $"DLCs faltantes: {string.Join(",", missing)}";
        }
        else if (e.CompatibleVersions.Count == 0 || e.DlcDependencies.Count == 0)
        {
            StatusColor = "neutral";
            StatusGlyph = "⚪";
            StatusTooltip = "Mod sin restricciones declaradas";
        }
        else
        {
            StatusColor = "ok";
            StatusGlyph = "🟢";
            StatusTooltip = "Compatible";
        }
    }

    private static string FormatSize(long b)
    {
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:0.0} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):0.0} MB";
        return $"{b / (1024.0 * 1024 * 1024):0.0} GB";
    }
}