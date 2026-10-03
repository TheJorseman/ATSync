using ATSync.Core.Models;

namespace ATSync.Core.Mods;

/// <summary>Valida que un mod sea utilizable en el juego del usuario (versión + DLCs).</summary>
public static class ModValidator
{
    public sealed class ValidationResult
    {
        public bool VersionOk { get; init; }
        public bool DlcsOk { get; init; }
        public IReadOnlyList<string> MissingDlcs { get; init; } = Array.Empty<string>();
        public bool Ok => VersionOk && DlcsOk;
        public string Summary => Ok ? "OK" : $"Version={VersionOk}, DLCs={DlcsOk}, MissingDlcs=[{string.Join(",", MissingDlcs)}]";
    }

    public static ValidationResult Validate(ModEntry mod, GameVersion installed, IReadOnlyCollection<string> ownedDlcIds)
    {
        var versionOk = mod.CompatibleVersions.Count == 0
            || mod.CompatibleVersions.Any(v => installed.MatchesWildcard(v));

        var missing = new List<string>();
        foreach (var d in mod.DlcDependencies)
            if (!ownedDlcIds.Contains(d)) missing.Add(d);

        return new ValidationResult
        {
            VersionOk = versionOk,
            DlcsOk = missing.Count == 0,
            MissingDlcs = missing
        };
    }
}