using ATSync.Core.Models;
using ATSync.Core.Util;

namespace ATSync.Core.Profiles;

/// <summary>Repositorio local de perfiles ATSync (en AppPaths.ProfilesDir).</summary>
public sealed class ProfileRepository
{
    public string ProfilesDir { get; }
    public ProfileRepository(string? profilesDir = null)
    {
        ProfilesDir = profilesDir ?? new AppPaths().ProfilesDir;
        Directory.CreateDirectory(ProfilesDir);
    }

    public IEnumerable<string> ListFiles()
        => Directory.Exists(ProfilesDir)
            ? Directory.EnumerateFiles(ProfilesDir, "*.atsync")
            : Enumerable.Empty<string>();

    public IEnumerable<AtsyncProfile> ListProfiles()
    {
        foreach (var f in ListFiles())
        {
            AtsyncProfile? p = null;
            try { p = ProfileSerializer.Deserialize(File.ReadAllText(f)); }
            catch { /* skip */ }
            if (p is not null) yield return p;
        }
    }

    public string Save(AtsyncProfile p)
    {
        Directory.CreateDirectory(ProfilesDir);
        var fname = SafeFileName(p.Name) + ".atsync";
        var path = Path.Combine(ProfilesDir, fname);
        File.WriteAllText(path, ProfileSerializer.Serialize(p));
        return path;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "profile" : clean;
    }
}