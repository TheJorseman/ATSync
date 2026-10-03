using System.Text.Json;
using System.Text.Json.Serialization;
using ATSync.Core.Models;

namespace ATSync.Core.Profiles;

/// <summary>Serializa y deserializa perfiles ATSync (JSON).</summary>
public static class ProfileSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(AtsyncProfile profile)
        => JsonSerializer.Serialize(profile, Options);

    public static AtsyncProfile Deserialize(string text)
    {
        var p = JsonSerializer.Deserialize<AtsyncProfile>(text, Options)
                ?? throw new InvalidDataException("Perfil vacío o inválido");
        if (string.IsNullOrEmpty(p.Id)) throw new InvalidDataException("Perfil sin `id`");
        if (string.IsNullOrEmpty(p.Name)) throw new InvalidDataException("Perfil sin `name`");
        return p;
    }
}