using ATSync.Core.Ats;
using ATSync.Core.Models;
using ATSync.Core.Mods;
using ATSync.Core.Profiles;
using ATSync.Core.Util;

namespace ATSync.Core.P2P;

/// <summary>Coordina la importación de un perfil desde un URI: descarga el perfil y cada mod, valida, mueve a la carpeta mod del juego.</summary>
public sealed class ProfileImporter
{
    private readonly PeerIdentity _identity;
    private readonly ProfileTransfer _transfer;
    private readonly ProfileRepository _repo;
    private readonly GameVersionDetector _gameVer;
    private readonly DlcDetector _dlcDet;
    private readonly AppPaths _paths;

    /// <summary>Si true, valida contra juego+DLCs del usuario. Si false, acepta cualquier perfil (útil en smoke tests sin ATS).</summary>
    public bool ValidateAgainstLocal { get; set; } = true;

    public ProfileImporter(
        PeerIdentity identity,
        ProfileTransfer transfer,
        ProfileRepository repo,
        GameVersionDetector gameVer,
        DlcDetector dlcDet,
        AppPaths paths)
    {
        _identity = identity;
        _transfer = transfer;
        _repo = repo;
        _gameVer = gameVer;
        _dlcDet = dlcDet;
        _paths = paths;
    }

    public sealed class ImportResult
    {
        public bool Success { get; init; }
        public AtsyncProfile? Profile { get; init; }
        public List<ModImportResult> Mods { get; init; } = new();
        public string? Error { get; init; }
    }

    public sealed class ModImportResult
    {
        public required string Filename { get; init; }
        public required bool Ok { get; init; }
        public required string LocalPath { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>Importa un perfil desde un URI `atsync://profile/&lt;cid&gt;` dado un multiaddr.</summary>
    public async Task<ImportResult> ImportAsync(string uri, string peerAddress, CancellationToken ct = default)
    {
        if (!ProfileUri.TryParse(uri, out var parsed) || parsed is null || parsed.Scheme != "profile")
            return new ImportResult { Error = $"URI inválido: {uri}" };

        var profile = await _transfer.DownloadProfileAsync(peerAddress, parsed.Cid, ct);
        if (profile is null)
            return new ImportResult { Error = "Perfil no encontrado en el peer remoto" };

        var ver = _gameVer.DetectFromGameLog();
        var dlcs = _dlcDet.Detect();
        var ownedDlcIds = dlcs.Where(d => d.Owned).Select(d => d.Id).ToHashSet();

        var results = new List<ModImportResult>();
        Directory.CreateDirectory(_paths.StagingDir);
        foreach (var m in profile.Mods)
        {
            try
            {
                var localPath = await _transfer.DownloadModAsync(peerAddress, m.Cid, _paths.StagingDir, ct);
                if (localPath is null)
                {
                    results.Add(new ModImportResult { Filename = m.Filename, Ok = false, LocalPath = "", Error = "mod no encontrado en peer" });
                    continue;
                }
                // Valida SHA-256
                var actual = Hashing.Sha256HexOfFile(localPath);
                if (!string.IsNullOrEmpty(m.Sha256) && !m.Sha256.Equals(actual, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new ModImportResult { Filename = m.Filename, Ok = false, LocalPath = localPath, Error = "sha256 mismatch" });
                    continue;
                }
                // Valida version + DLCs si las tenemos (y si ValidateAgainstLocal está activo)
                if (ValidateAgainstLocal)
                {
                    if (ver is not null)
                    {
                        var installed = ver;
                        var versionOk = m.CompatibleVersions.Count == 0 || m.CompatibleVersions.Any(v => installed.MatchesWildcard(v));
                        if (!versionOk)
                        {
                            results.Add(new ModImportResult { Filename = m.Filename, Ok = false, LocalPath = localPath, Error = "versión incompatible" });
                            continue;
                        }
                    }
                    if (m.DlcDependencies.Count > 0 && m.DlcDependencies.Any(d => !ownedDlcIds.Contains(d)))
                    {
                        var missing = m.DlcDependencies.Where(d => !ownedDlcIds.Contains(d)).ToList();
                        results.Add(new ModImportResult { Filename = m.Filename, Ok = false, LocalPath = localPath, Error = $"DLCs faltantes: {string.Join(",", missing)}" });
                        continue;
                    }
                }
                // Mueve a la carpeta mod del juego
                var dest = Path.Combine(AppPaths.DefaultAtsModsDir(), Path.GetFileName(localPath));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(localPath, dest);
                results.Add(new ModImportResult { Filename = m.Filename, Ok = true, LocalPath = dest });
            }
            catch (Exception ex)
            {
                results.Add(new ModImportResult { Filename = m.Filename, Ok = false, LocalPath = "", Error = ex.Message });
            }
        }

        _repo.Save(profile);

        return new ImportResult
        {
            Success = results.All(r => r.Ok),
            Profile = profile,
            Mods = results
        };
    }
}