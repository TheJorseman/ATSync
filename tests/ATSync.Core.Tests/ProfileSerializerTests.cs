using ATSync.Core.Models;
using ATSync.Core.Profiles;

namespace ATSync.Core.Tests;

public class ProfileSerializerTests
{
    [Fact]
    public void RoundtripProfile()
    {
        var profile = new AtsyncProfile
        {
            Id = "12D3KooWTest",
            Name = "Test Profile",
            Target = new ProfileTarget
            {
                Game = "ats",
                Version = "1.61.*",
                DlcsRequired = new() { "dlc_arizona", "dlc_south_dakota" }
            },
            Mods = new()
            {
                new ProfileModEntry
                {
                    Filename = "test.scs",
                    Size = 1024,
                    Sha256 = "abcd1234",
                    Cid = "bafkreitest",
                    LoadOrder = 10,
                    DisplayName = "Test Mod",
                    CompatibleVersions = new() { "1.61.*" }
                }
            }
        };

        var json = ProfileSerializer.Serialize(profile);
        var back = ProfileSerializer.Deserialize(json);

        Assert.Equal(profile.Name, back.Name);
        Assert.Equal(profile.Id, back.Id);
        Assert.Equal(profile.Target.Version, back.Target.Version);
        Assert.Equal(profile.Target.DlcsRequired, back.Target.DlcsRequired);
        Assert.Single(back.Mods);
        Assert.Equal("test.scs", back.Mods[0].Filename);
        Assert.Equal("bafkreitest", back.Mods[0].Cid);
    }

    [Fact]
    public void DeserializeFailsOnInvalid()
    {
        Assert.Throws<InvalidDataException>(() => ProfileSerializer.Deserialize("{}"));
    }
}