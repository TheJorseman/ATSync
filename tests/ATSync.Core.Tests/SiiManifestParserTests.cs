using ATSync.Core.Mods;

namespace ATSync.Core.Tests;

public class SiiManifestParserTests
{
    [Fact]
    public void ParsesBasicManifest()
    {
        var text = """
            SiiNunit {
                mod_package : .promods {
                    package_version: "2.6.1"
                    display_name: "ProMods ATS"
                    author: "ProMods Team"
                    category[]: "map"
                    compatible_versions[]: "1.60.*"
                    compatible_versions[]: "1.61.*"
                    dlc_dependencies[]: "dlc_arizona"
                    mp_mod_optional: true
                }
            }
            """;
        var m = SiiManifestParser.Parse(text);
        Assert.Equal("2.6.1", m.PackageVersion);
        Assert.Equal("ProMods ATS", m.DisplayName);
        Assert.Equal("ProMods Team", m.Author);
        Assert.Contains("map", m.Categories);
        Assert.Equal(new[] { "1.60.*", "1.61.*" }, m.CompatibleVersions);
        Assert.Equal(new[] { "dlc_arizona" }, m.DlcDependencies);
        Assert.True(m.MpModOptional);
    }

    [Fact]
    public void ParsesEmptyManifestGracefully()
    {
        var m = SiiManifestParser.Parse("");
        Assert.Equal("", m.DisplayName);
        Assert.Empty(m.Categories);
        Assert.True(m.Warnings.Any(w => w.Contains("display_name")));
        Assert.True(m.Warnings.Any(w => w.Contains("compatible_versions")));
    }

    [Fact]
    public void IgnoresUnknownKeys()
    {
        var text = """
            SiiNunit { mod_package : .x {
                display_name: "X"
                some_unknown_key: "value"
                weird_array[]: "y"
            }}
            """;
        var m = SiiManifestParser.Parse(text);
        Assert.Equal("X", m.DisplayName);
        Assert.NotEmpty(m.Warnings);
    }

    [Fact]
    public void IgnoresCommentsAndBlankLines()
    {
        var text = """
            // comentario
            SiiNunit { mod_package : .x {
                # another comment
                display_name: "X"

                package_version: "1"
            }}
            """;
        var m = SiiManifestParser.Parse(text);
        Assert.Equal("X", m.DisplayName);
        Assert.Equal("1", m.PackageVersion);
    }
}