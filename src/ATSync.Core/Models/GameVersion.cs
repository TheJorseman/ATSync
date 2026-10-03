namespace ATSync.Core.Models;

/// <summary>Versión del juego (p.ej. 1.61.0.5s). Inmutable.</summary>
public sealed class GameVersion : IEquatable<GameVersion>, IComparable<GameVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Build { get; }
    public bool IsStable { get; }       // true si termina en 's'

    public GameVersion(int major, int minor, int patch, int build, bool isStable)
    {
        Major = major; Minor = minor; Patch = patch; Build = build; IsStable = isStable;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}.{Build}{(IsStable ? 's' : 'e')}";

    /// <summary>Parsea "1.61.0.5s" o "1.61.0.5e" o "1.61" (sin suffix).</summary>
    public static GameVersion Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var t = text.Trim();
        var isStable = true;
        if (t.EndsWith('s')) isStable = true;
        else if (t.EndsWith('e')) isStable = false;
        var numeric = t.TrimEnd('s', 'e');
        var parts = numeric.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int[] nums = new int[4];
        for (int i = 0; i < 4; i++)
            nums[i] = i < parts.Length && int.TryParse(parts[i], out var v) ? v : 0;
        return new GameVersion(nums[0], nums[1], nums[2], nums[3], isStable);
    }

    public bool Equals(GameVersion? other) => other is not null && ToString() == other.ToString();
    public override bool Equals(object? obj) => obj is GameVersion v && Equals(v);
    public override int GetHashCode() => ToString().GetHashCode();
    public int CompareTo(GameVersion? other) => other is null ? 1 : string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Coincide contra un patrón con wildcards `*` por segmento (p.ej. "1.61.*", "1.6*.*", "*").
    /// Soporta que el patrón tenga menos segmentos que la versión.
    /// </summary>
    public bool MatchesWildcard(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*") return true;
        var pParts = pattern.Trim().Split('.');
        var vParts = new[] { Major.ToString(), Minor.ToString(), Patch.ToString(), Build.ToString() };
        for (int i = 0; i < pParts.Length; i++)
        {
            var p = pParts[i].Trim();
            if (p == "*") continue;
            if (i >= 4) return false;
            if (!WildcardMatch(p, vParts[i])) return false;
        }
        return true;
    }

    private static bool WildcardMatch(string pattern, string input)
    {
        // soporta sólo '*' como sufijo (suficiente para versiones)
        if (pattern.EndsWith('*'))
            return input.StartsWith(pattern[..^1], StringComparison.Ordinal);
        return pattern == input;
    }
}