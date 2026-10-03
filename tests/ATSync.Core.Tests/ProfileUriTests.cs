using ATSync.Core.P2P;

namespace ATSync.Core.Tests;

public class ProfileUriTests
{
    [Fact]
    public void ParsesProfileUri()
    {
        var uri = ProfileUri.Parse("atsync://profile/bafkreitest123");
        Assert.Equal("profile", uri.Scheme);
        Assert.Equal("bafkreitest123", uri.Cid);
    }

    [Fact]
    public void ParsesModUri()
    {
        var uri = ProfileUri.Parse("atsync://mod/bafkreimod");
        Assert.Equal("mod", uri.Scheme);
    }

    [Theory]
    [InlineData("")]
    [InlineData("magnet:?xt=urn:btih:foo")]
    [InlineData("atsync://unknown/foo")]
    [InlineData("atsync://profile/")]
    public void InvalidUriThrows(string text)
    {
        Assert.False(ProfileUri.TryParse(text, out _));
    }
}