namespace ATSync.Core.P2P;

/// <summary>
/// URI de un perfil ATSync (formato custom).
/// Formato: atsync://profile/&lt;profile_cid&gt;  ó  atsync://mod/&lt;mod_cid&gt;
/// </summary>
public sealed class ProfileUri
{
    public required string Scheme { get; init; }                // "profile" | "mod"
    public required string Cid { get; init; }

    public override string ToString() => $"atsync://{Scheme}/{Cid}";

    public static ProfileUri Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!text.StartsWith("atsync://", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"URI no es atsync:// ({text})");
        var rest = text["atsync://".Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
            throw new FormatException($"URI mal formado: {text}");
        var scheme = rest[..slash].ToLowerInvariant();
        var cid = rest[(slash + 1)..];
        if (scheme != "profile" && scheme != "mod")
            throw new FormatException($"Esquema desconocido: {scheme}");
        return new ProfileUri { Scheme = scheme, Cid = cid };
    }

    public static bool TryParse(string? text, out ProfileUri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try { uri = Parse(text); return true; }
        catch { return false; }
    }
}