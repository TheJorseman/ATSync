namespace ATSync.Core.Models;

/// <summary>Perfil ATSync — un grupo de mods con requisitos de versión y DLCs.</summary>
public sealed class AtsyncProfile
{
    public string Schema { get; set; } = "atsync.profile/v1";
    public string Id { get; set; } = "";                     // libp2p PeerID del autor
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ProfileTarget Target { get; set; } = new();
    public List<ProfileModEntry> Mods { get; set; } = new();
}

public sealed class ProfileTarget
{
    public string Game { get; set; } = "ats";                // "ats"
    public string Version { get; set; } = "1.61.*";          // wildcard
    public List<string> DlcsRequired { get; set; } = new();
}

public sealed class ProfileModEntry
{
    public string Filename { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Cid { get; set; } = "";                    // bafy...
    public int LoadOrder { get; set; }
    public string DisplayName { get; set; } = "";
    public string PackageVersion { get; set; } = "";
    public List<string> CompatibleVersions { get; set; } = new();
    public List<string> DlcDependencies { get; set; } = new();
    public string Source { get; set; } = "local";
}