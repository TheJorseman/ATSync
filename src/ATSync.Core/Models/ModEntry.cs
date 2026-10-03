namespace ATSync.Core.Models;

/// <summary>
/// Entrada de mod ATS parseada del manifest.sii o construida localmente.
/// </summary>
public sealed record ModEntry
{
    public required string Filename { get; init; }          // "modname.scs"
    public required string FullPath { get; init; }          // ruta absoluta local
    public long Size { get; init; }
    public string Sha256Hex { get; init; } = "";            // calculado perezosamente por ModScanner

    public string DisplayName { get; init; } = "";
    public string Author { get; init; } = "";
    public string PackageVersion { get; init; } = "";
    public string DescriptionFile { get; init; } = "";
    public string IconPath { get; init; } = "";

    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CompatibleVersions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DlcDependencies { get; init; } = Array.Empty<string>();
    public bool MpModOptional { get; init; }
}