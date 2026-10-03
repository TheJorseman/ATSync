using ATSync.Core.Models;

namespace ATSync.Core.Ats;

/// <summary>
/// Tabla bundled con todos los DLCs de ATS conocidos a 2026-10-03.
/// </summary>
public static class DlcCatalog
{
    public static IReadOnlyList<DlcInfo> All { get; } = new DlcInfo[]
    {
        // Mapas
        new() { Id="dlc_arizona",       Name="Arizona",                  SteamAppId=377541, ManifestName="arizona",       Category="map" },
        new() { Id="dlc_nm",           Name="New Mexico",               SteamAppId=491290, ManifestName="new_mexico",    Category="map" },
        new() { Id="dlc_oregon",       Name="Oregon",                   SteamAppId=526510, ManifestName="oregon",         Category="map" },
        new() { Id="dlc_washington",   Name="Washington",               SteamAppId=526520, ManifestName="washington",     Category="map" },
        new() { Id="dlc_utah",         Name="Utah",                     SteamAppId=526530, ManifestName="utah",           Category="map" },
        new() { Id="dlc_idaho",        Name="Idaho",                    SteamAppId=556660, ManifestName="idaho",          Category="map" },
        new() { Id="dlc_colorado",     Name="Colorado",                 SteamAppId=556670, ManifestName="colorado",       Category="map" },
        new() { Id="dlc_wyoming",      Name="Wyoming",                  SteamAppId=594571, ManifestName="wyoming",        Category="map" },
        new() { Id="dlc_montana",      Name="Montana",                  SteamAppId=594572, ManifestName="montana",        Category="map" },
        new() { Id="dlc_texas",        Name="Texas",                    SteamAppId=680030, ManifestName="texas",         Category="map" },
        new() { Id="dlc_oklahoma",     Name="Oklahoma",                 SteamAppId=680040, ManifestName="oklahoma",       Category="map" },
        new() { Id="dlc_kansas",       Name="Kansas",                   SteamAppId=715220, ManifestName="kansas",         Category="map" },
        new() { Id="dlc_nebraska",     Name="Nebraska",                 SteamAppId=715221, ManifestName="nebraska",       Category="map" },
        new() { Id="dlc_arkansas",     Name="Arkansas",                 SteamAppId=770100, ManifestName="arkansas",       Category="map" },
        new() { Id="dlc_missouri",     Name="Missouri",                 SteamAppId=770101, ManifestName="missouri",       Category="map" },
        new() { Id="dlc_iowa",         Name="Iowa",                     SteamAppId=840800, ManifestName="iowa",           Category="map" },
        new() { Id="dlc_louisiana",    Name="Louisiana",                SteamAppId=840801, ManifestName="louisiana",      Category="map" },
        new() { Id="dlc_illinois",     Name="Illinois",                 SteamAppId=910730, ManifestName="illinois",       Category="map" },
        new() { Id="dlc_south_dakota", Name="South Dakota",             SteamAppId=2837960, ManifestName="south_dakota", Category="map" },

        // Road Trip
        new() { Id="dlc_roadtrip_ford", Name="Road Trip: Ford",          SteamAppId=2890000, ManifestName="roadtrip_ford", Category="road_trip" },

        // Cargo
        new() { Id="dlc_heavy_cargo",   Name="Heavy Cargo Pack",         SteamAppId=381140, ManifestName="heavy_cargo",    Category="cargo" },
        new() { Id="dlc_special",       Name="Special Transport",        SteamAppId=502660, ManifestName="special",        Category="cargo" },
        new() { Id="dlc_forest",        Name="Forest Machinery",         SteamAppId=552100, ManifestName="forest_machinery", Category="cargo" },
        new() { Id="dlc_farm",          Name="Farm Machinery",           SteamAppId=552110, ManifestName="farm_machinery", Category="cargo" },
        new() { Id="dlc_volvoconstr",   Name="Volvo Construction Equipment", SteamAppId=680050, ManifestName="vce",        Category="cargo" },
        new() { Id="dlc_jcb",           Name="JCB Equipment",            SteamAppId=910740, ManifestName="jcb",            Category="cargo" },
        new() { Id="dlc_krone",         Name="KRONE Agriculture",        SteamAppId=2497870, ManifestName="krone",         Category="cargo" },
        new() { Id="dlc_bobcat",        Name="Bobcat Cargo Pack",        SteamAppId=2894000, ManifestName="bobcat",        Category="cargo" },

        // Tuning / paint
        new() { Id="dlc_steampunk",     Name="Steampunk Pack",           SteamAppId=377530, ManifestName="steampunk",      Category="paint" },
        new() { Id="dlc_wheels",        Name="Wheel Tuning Pack",         SteamAppId=377540, ManifestName="wheel_tuning",   Category="tuning" },
        new() { Id="dlc_steering",      Name="Steering Creations Pack",   SteamAppId=466950, ManifestName="steering",       Category="tuning" },
        new() { Id="dlc_dragon",        Name="Dragon Truck Design Pack",  SteamAppId=556680, ManifestName="dragon",         Category="paint" },
        new() { Id="dlc_halloween",     Name="Halloween Paint Jobs Pack", SteamAppId=715230, ManifestName="halloween",      Category="paint" },
        new() { Id="dlc_christmas",     Name="Christmas Paint Jobs Pack", SteamAppId=715231, ManifestName="christmas",      Category="paint" },
        new() { Id="dlc_valentines",    Name="Valentine's Paint Jobs Pack", SteamAppId=715232, ManifestName="valentines",    Category="paint" },
        new() { Id="dlc_classic",       Name="Classic Stripes Paint Jobs Pack", SteamAppId=910731, ManifestName="classic_stripes", Category="paint" },
        new() { Id="dlc_space",         Name="Space Paint Jobs Pack",     SteamAppId=2697100, ManifestName="space",          Category="paint" },

        // Trucks
        new() { Id="dlc_t680",          Name="Kenworth T680",            SteamAppId=304730, ManifestName="t680",           Category="truck" },
        new() { Id="dlc_p579",          Name="Peterbilt 579",            SteamAppId=304731, ManifestName="p579",           Category="truck" },
        new() { Id="dlc_vnl2014",       Name="Volvo VNL 2014",           SteamAppId=345110, ManifestName="vnl_2014",       Category="truck" },
        new() { Id="dlc_w900",          Name="Kenworth W900",             SteamAppId=388800, ManifestName="w900",           Category="truck" },
        new() { Id="dlc_p389",          Name="Peterbilt 389",            SteamAppId=388810, ManifestName="p389",           Category="truck" },
        new() { Id="dlc_lonestar",      Name="International LoneStar",   SteamAppId=520520, ManifestName="lonestar",       Category="truck" },
        new() { Id="dlc_anthem",        Name="Mack Anthem",              SteamAppId=680031, ManifestName="anthem",         Category="truck" },
        new() { Id="dlc_intlt",         Name="International LT",          SteamAppId=2497880, ManifestName="intl_lt",        Category="truck" },
    };

    public static DlcInfo? FindById(string id) => All.FirstOrDefault(d => d.Id == id);
}