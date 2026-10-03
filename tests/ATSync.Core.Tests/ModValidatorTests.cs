using ATSync.Core.Models;
using ATSync.Core.Mods;

namespace ATSync.Core.Tests;

public class ModValidatorTests
{
    [Fact]
    public void ValidatesCompatibleVersion()
    {
        var m = new ModEntry
        {
            Filename = "x.scs",
            FullPath = "/x.scs",
            CompatibleVersions = new[] { "1.61.*", "1.60.*" }
        };
        var ver = GameVersion.Parse("1.61.0.5s");
        var r = ModValidator.Validate(m, ver, Array.Empty<string>());
        Assert.True(r.VersionOk);
    }

    [Fact]
    public void DetectsIncompatibleVersion()
    {
        var m = new ModEntry
        {
            Filename = "x.scs",
            FullPath = "/x.scs",
            CompatibleVersions = new[] { "1.55.*" }
        };
        var r = ModValidator.Validate(m, GameVersion.Parse("1.61.0.5s"), Array.Empty<string>());
        Assert.False(r.VersionOk);
    }

    [Fact]
    public void DetectsMissingDlc()
    {
        var m = new ModEntry
        {
            Filename = "x.scs",
            FullPath = "/x.scs",
            CompatibleVersions = new[] { "*" },
            DlcDependencies = new[] { "dlc_arizona" }
        };
        var r = ModValidator.Validate(m, GameVersion.Parse("1.61.0.5s"), new[] { "dlc_oregon" });
        Assert.True(r.VersionOk);
        Assert.False(r.DlcsOk);
        Assert.Contains("dlc_arizona", r.MissingDlcs);
    }
}