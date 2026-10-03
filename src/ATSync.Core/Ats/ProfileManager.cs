namespace ATSync.Core.Ats;

/// <summary>
/// Lee y modifica los archivos de perfil del juego (`profiles.cfg`, `mod.sii` por profile).
/// Crea backups automáticos antes de modificar `mod.sii`.
/// </summary>
public sealed class ProfileManager
{
    public string AtsHomeDir { get; }
    public string ProfilesDir => Path.Combine(AtsHomeDir, "profiles");

    public ProfileManager(string? atsHomeDir = null)
    {
        AtsHomeDir = atsHomeDir ?? Core.Util.AppPaths.DefaultAtsHomeDir();
    }

    public IEnumerable<AtsProfile> ListProfiles()
    {
        var cfg = Path.Combine(AtsHomeDir, "profiles.cfg");
        if (!File.Exists(cfg)) yield break;
        foreach (var raw in File.ReadAllLines(cfg))
        {
            var t = NormalizeLine(raw);
            if (t.Length == 0) continue;
            var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var id = parts[^1];
            var dir = Path.Combine(ProfilesDir, id);
            if (!Directory.Exists(dir)) continue;
            var name = parts.Length >= 3 ? parts[0] : id;
            yield return new AtsProfile(id, dir, name);
        }
    }

    private static string NormalizeLine(string l) => l.Trim().TrimStart('"').TrimEnd('"');

    /// <summary>Devuelve el archivo mod.sii de un perfil, o null si no existe.</summary>
    public string? GetModSiiPath(string profileId)
        => Path.Combine(ProfilesDir, profileId, "mod.sii");

    /// <summary>Lee el `mod.sii` del perfil y devuelve la lista de mods en orden.</summary>
    public List<string> ReadActiveMods(string profileId)
    {
        var modSii = GetModSiiPath(profileId);
        if (modSii is null || !File.Exists(modSii)) return new();
        var text = File.ReadAllText(modSii);
        return ParseModList(text);
    }

    /// <summary>Hace backup de mod.sii y lo reescribe con la lista de mods indicada.</summary>
    public void WriteActiveMods(string profileId, IEnumerable<string> modsInOrder)
    {
        var path = GetModSiiPath(profileId);
        if (path is null) throw new InvalidOperationException($"Perfil no encontrado: {profileId}");

        if (File.Exists(path))
            File.Copy(path, path + ".bak", overwrite: true);
        else
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, SerializeModList(profileId, modsInOrder));
    }

    private static List<string> ParseModList(string text)
    {
        var list = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            // Formato: mod[]: "<hash>|<path>"
            var ix = line.IndexOf(':');
            if (ix < 0) continue;
            var key = line[..ix].Trim();
            if (!key.StartsWith("mod")) continue;
            var valStart = line.IndexOf('"', ix);
            if (valStart < 0) continue;
            var valEnd = line.LastIndexOf('"');
            if (valEnd <= valStart) continue;
            list.Add(line.Substring(valStart + 1, valEnd - valStart - 1));
        }
        return list;
    }

    private static string SerializeModList(string profileId, IEnumerable<string> mods)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SiiNunit {");
        sb.Append("  mod_set : .<").Append(profileId).AppendLine("> {");
        var i = 0;
        foreach (var m in mods)
        {
            sb.Append("    mod[").Append(i++).Append("]: \"").Append(m).AppendLine("\"");
        }
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public sealed record AtsProfile(string Id, string Directory, string Name);
}