using ATSync.Core.Models;

namespace ATSync.Core.Tests;

public class GameVersionTests
{
    [Fact]
    public void ParsesStableVersion()
    {
        var v = GameVersion.Parse("1.61.0.5s");
        Assert.Equal(1, v.Major);
        Assert.Equal(61, v.Minor);
        Assert.Equal(0, v.Patch);
        Assert.Equal(5, v.Build);
        Assert.True(v.IsStable);
        Assert.Equal("1.61.0.5s", v.ToString());
    }

    [Fact]
    public void ParsesExperimentalVersion()
    {
        var v = GameVersion.Parse("1.62.0.1e");
        Assert.False(v.IsStable);
    }

    [Fact]
    public void MatchesWildcardSegment()
    {
        var v = GameVersion.Parse("1.61.0.5s");
        Assert.True(v.MatchesWildcard("1.61.*"));
        Assert.True(v.MatchesWildcard("1.6*.*"));
        Assert.True(v.MatchesWildcard("*"));
        Assert.False(v.MatchesWildcard("1.60.*"));
    }

    [Fact]
    public void CompareOrders()
    {
        var a = GameVersion.Parse("1.61.0.5s");
        var b = GameVersion.Parse("1.61.0.6s");
        Assert.True(a.CompareTo(b) < 0);
        Assert.Equal(0, a.CompareTo(a));
    }
}