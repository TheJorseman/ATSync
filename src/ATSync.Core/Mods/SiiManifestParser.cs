using System.Text.RegularExpressions;

namespace ATSync.Core.Mods;

/// <summary>
/// Parser tolerante del `manifest.sii` que SCS usa en cada .scs.
/// No implementa SiiNunit completo; sólo extrae los campos que necesitamos.
/// </summary>
public static class SiiManifestParser
{
    private static readonly Regex RxScalarString = new(@"^(\w+)\s*:\s*""([^""]*)""\s*$", RegexOptions.Compiled);
    private static readonly Regex RxScalarBool   = new(@"^(\w+)\s*:\s*(true|false)\s*$", RegexOptions.Compiled);
    private static readonly Regex RxArray       = new(@"^(\w+)\s*\[\]\s*:\s*(.+?)\s*$", RegexOptions.Compiled);

    public sealed class ParsedManifest
    {
        public string PackageVersion { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Author { get; set; } = "";
        public string Icon { get; set; } = "";
        public string DescriptionFile { get; set; } = "";
        public bool MpModOptional { get; set; }
        public List<string> Categories { get; } = new();
        public List<string> CompatibleVersions { get; } = new();
        public List<string> DlcDependencies { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    public static ParsedManifest Parse(string text)
    {
        var m = new ParsedManifest();
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith('#')) continue;

                // arrays: category[]: "truck"
                var arrayMatch = RxArray.Match(line);
                if (arrayMatch.Success)
                {
                    var key = arrayMatch.Groups[1].Value;
                    var values = ExtractStringList(arrayMatch.Groups[2].Value);
                    switch (key)
                    {
                        case "category":            m.Categories.AddRange(values); break;
                        case "compatible_versions": m.CompatibleVersions.AddRange(values); break;
                        case "dlc_dependencies":    m.DlcDependencies.AddRange(values); break;
                        default: m.Warnings.Add($"Unknown array key: {key}"); break;
                    }
                    continue;
                }

                var strMatch = RxScalarString.Match(line);
                if (strMatch.Success)
                {
                    var key = strMatch.Groups[1].Value;
                    var val = strMatch.Groups[2].Value;
                    switch (key)
                    {
                        case "package_version":    m.PackageVersion = val; break;
                        case "display_name":       m.DisplayName = val; break;
                        case "author":             m.Author = val; break;
                        case "icon":               m.Icon = val; break;
                        case "description_file":   m.DescriptionFile = val; break;
                    }
                    continue;
                }

                var boolMatch = RxScalarBool.Match(line);
                if (boolMatch.Success && boolMatch.Groups[1].Value == "mp_mod_optional")
                {
                    m.MpModOptional = boolMatch.Groups[2].Value == "true";
                }
            }
        }

        if (string.IsNullOrEmpty(m.DisplayName))
            m.Warnings.Add("manifest.sii sin `display_name`");
        if (m.CompatibleVersions.Count == 0)
            m.Warnings.Add("manifest.sii sin `compatible_versions[]`");

        return m;
    }

    /// <summary>Extrae una lista de strings `"a", "b", "c"` o `["a", "b"]`.</summary>
    private static List<string> ExtractStringList(string raw)
    {
        var result = new List<string>();
        foreach (Match m in Regex.Matches(raw, @"""([^""]*)"""))
            result.Add(m.Groups[1].Value);
        return result;
    }
}