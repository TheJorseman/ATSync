using ATSync.Core.Models;

namespace ATSync.App.ViewModels;

public sealed class ProfileView
{
    public AtsyncProfile Profile { get; }
    public string Name => Profile.Name;
    public string Author => TruncatePeerId(Profile.Id);
    public DateTimeOffset CreatedAt => Profile.CreatedAt;
    public int ModCount => Profile.Mods.Count;
    public long TotalSize => Profile.Mods.Sum(m => m.Size);
    public string SizeText => FormatSize(TotalSize);
    public string GameVersion => Profile.Target.Version;
    public int DlcCount => Profile.Target.DlcsRequired.Count;
    public string Subtitle => $"v{Profile.Target.Version} · {ModCount} mods · {SizeText} · {DlcCount} DLCs";

    public ProfileView(AtsyncProfile p) { Profile = p; }

    private static string TruncatePeerId(string id) =>
        string.IsNullOrEmpty(id) || id.Length < 16 ? id ?? "—" : id[..12] + "…" + id[^4..];

    private static string FormatSize(long b)
    {
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:0.0} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):0.0} MB";
        return $"{b / (1024.0 * 1024 * 1024):0.0} GB";
    }
}