using ATSync.Core.Ats;
using ATSync.Core.Models;
using ATSync.Core.Mods;
using ATSync.Core.P2P;

namespace ATSync.Core.Profiles;

/// <summary>Construye un AtsyncProfile a partir de mods seleccionados por el usuario.</summary>
public static class ProfileBuilder
{
    public static AtsyncProfile Build(
        string name,
        IReadOnlyList<ModEntry> selectedMods,
        PeerIdentity author,
        GameVersion gameVersion,
        IReadOnlyCollection<string> ownedDlcIds)
    {
        var profile = new AtsyncProfile
        {
            Id = author.PeerId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            Target = new ProfileTarget
            {
                Game = "ats",
                Version = gameVersion.ToString()[..2] + ".*",
                DlcsRequired = ownedDlcIds.ToList()
            }
        };

        var order = 10;
        foreach (var m in selectedMods)
        {
            profile.Mods.Add(new ProfileModEntry
            {
                Filename = m.Filename,
                Size = m.Size,
                Sha256 = m.Sha256Hex,
                Cid = string.IsNullOrEmpty(m.Sha256Hex) ? "" : CidBuilder.ComputeCidOfFile(m.FullPath),
                LoadOrder = order,
                DisplayName = m.DisplayName,
                PackageVersion = m.PackageVersion,
                CompatibleVersions = m.CompatibleVersions.ToList(),
                DlcDependencies = m.DlcDependencies.ToList(),
                Source = "local"
            });
            order += 10;
        }
        return profile;
    }
}