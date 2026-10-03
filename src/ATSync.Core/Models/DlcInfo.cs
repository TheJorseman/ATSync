namespace ATSync.Core.Models;

/// <summary>Información de un DLC ATS disponible / poseído por el usuario.</summary>
public sealed class DlcInfo
{
    public required string Id { get; init; }                  // "dlc_arizona"
    public required string Name { get; init; }                // "Arizona"
    public required uint SteamAppId { get; init; }           // 377541
    public required string ManifestName { get; init; }       // "arizona"
    public bool Owned { get; set; }
    public string Category { get; init; } = "map";           // "map" | "cargo" | "tuning" | "truck" | "paint" | "road_trip"
}