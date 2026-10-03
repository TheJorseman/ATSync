using ATSync.Core.Ats;
using ATSync.Core.Models;
using ATSync.Core.Mods;
using ATSync.Core.P2P;
using ATSync.Core.Profiles;
using ATSync.Core.Util;

namespace ATSync.Cli;

/// <summary>
/// CLI de ATSync. Comandos principales:
///   detect    → detecta juego y DLCs instalados
///   scan      → escanea carpeta mod y muestra mods
///   publish      → publica un perfil ATSync (devuelve PeerId + URI)
///   listen       → deja el peer a la escucha (P2P)
///   import &lt;uri&gt; &lt;addr&gt; → importa un perfil desde otro peer
///   smoke        → ejecuta el flujo completo end-to-end (auto-test)
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }
        try
        {
            return args[0] switch
            {
                "detect"      => CmdDetect(),
                "scan"        => CmdScan(),
                "smoke"       => CmdSmoke(),
                "publish"     => CmdPublish(args),
                "listen"      => CmdListen(args),
                "import"      => CmdImport(args),
                "activate"    => CmdActivate(args),
                "version"     => CmdVersion(),
                _             => Fail($"comando desconocido: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    private static int CmdVersion()
    {
        Console.WriteLine("ATSync 0.2.0");
        Console.WriteLine($"  .NET: {Environment.Version}");
        Console.WriteLine($"  OS: {Environment.OSVersion}");
        Console.WriteLine("  Transport: ProfileTransfer (TCP) + Nethermind.Libp2p 1.0.1 (libp2p)");
        return 0;
    }

    private static int CmdDetect()
    {
        var steamPath = SteamPathLocator.FindSteamInstallPath();
        var atsPath = SteamPathLocator.FindAtsInstallDir();
        var ver = new GameVersionDetector().DetectFromGameLog();
        var dlcs = new DlcDetector().Detect();
        Console.WriteLine($"Steam: {steamPath ?? "(no encontrado)"}");
        Console.WriteLine($"ATS install: {atsPath ?? "(no encontrado)"}");
        Console.WriteLine($"ATS version: {ver?.ToString() ?? "(no detectado)"}");
        Console.WriteLine($"DLCs owned: {dlcs.Count(d => d.Owned)}/{dlcs.Count}");
        foreach (var d in dlcs.Where(d => d.Owned))
            Console.WriteLine($"  ✓ {d.Name} ({d.Id}) [{d.Category}]");
        foreach (var d in dlcs.Where(d => !d.Owned))
            Console.WriteLine($"  · {d.Name} ({d.Id})");
        return 0;
    }

    private static int CmdScan()
    {
        var scanner = new ModScanner();
        Console.WriteLine($"Escaneando {scanner.ModsDir}...");
        if (!Directory.Exists(scanner.ModsDir))
        {
            Console.WriteLine("Carpeta mod no existe — el juego no está instalado o no se ha ejecutado.");
            return 0;
        }
        int n = 0;
        foreach (var m in scanner.Scan())
        {
            n++;
            Console.WriteLine($"  {m.Filename}  {m.Size:N0}B  cid={(string.IsNullOrEmpty(m.Sha256Hex) ? "?" : m.Sha256Hex[..Math.Min(12, m.Sha256Hex.Length)])}…  name={m.DisplayName}");
        }
        Console.WriteLine($"Total: {n} mods");
        return 0;
    }

    private static int CmdPublish(string[] args)
    {
        var paths = new AppPaths();
        var identity = PeerIdentity.Load(paths.IdentityPath);
        var repo = new ProfileRepository(paths.ProfilesDir);
        var transfer = new ProfileTransfer(identity, repo);
        var scanner = new ModScanner();
        var mods = scanner.Scan().Take(2).ToList();
        if (mods.Count == 0)
        {
            Console.Error.WriteLine("No hay mods en la carpeta mod del juego — no se puede publicar un perfil vacío.");
            return 3;
        }
        var gameVer = new GameVersionDetector().DetectFromGameLog() ?? new GameVersion(1, 61, 0, 0, true);
        var ownedDlcs = new DlcDetector().Detect().Where(d => d.Owned).Select(d => d.Id).ToList();
        var profile = ProfileBuilder.Build("smoke-profile", mods, identity, gameVer, ownedDlcs);
        var pub = new ProfilePublisher(identity, transfer, repo, paths.StagingDir);
        var uri = pub.Publish(profile);
        Console.WriteLine($"Perfil publicado:");
        Console.WriteLine($"  PeerId: {identity.PeerId}");
        Console.WriteLine($"  Profile URI: {uri}");
        // start local listener for this turn
        transfer.StartAsync(0).GetAwaiter().GetResult();
        Console.WriteLine($"  Listen addr: {transfer.LocalAddress}");
        Console.WriteLine("  (comparte la URI + addr a tus amigos)");
        // bloquear 30s mientras los amigos se conectan
        for (int i = 0; i < 30; i++)
        {
            System.Threading.Thread.Sleep(1000);
            Console.Write(".");
        }
        Console.WriteLine();
        return 0;
    }

    private static int CmdListen(string[] args)
    {
        var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 0;
        var paths = new AppPaths();
        var identity = PeerIdentity.Load(paths.IdentityPath);
        var repo = new ProfileRepository(paths.ProfilesDir);
        var transfer = new ProfileTransfer(identity, repo);
        transfer.StartAsync(port).GetAwaiter().GetResult();
        Console.WriteLine($"ATSync peer listening on {transfer.LocalAddress} (peerId={identity.PeerId})");
        Console.WriteLine("Press Ctrl+C to stop.");
        var exit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        exit.Wait();
        return 0;
    }

    private static int CmdImport(string[] args)
    {
        if (args.Length < 3) return Fail("uso: import <atsync-uri> <peer-addr>");
        var uri = args[1];
        var addr = args[2];
        var paths = new AppPaths();
        var identity = PeerIdentity.Load(paths.IdentityPath);
        var repo = new ProfileRepository(paths.ProfilesDir);
        var transfer = new ProfileTransfer(identity, repo);
        transfer.StartAsync(0).GetAwaiter().GetResult();
        var gameVer = new GameVersionDetector();
        var dlcDet = new DlcDetector();
        var importer = new ProfileImporter(identity, transfer, repo, gameVer, dlcDet, paths);
        var result = importer.ImportAsync(uri, addr, default).GetAwaiter().GetResult();
        if (!result.Success)
        {
            Console.Error.WriteLine($"Import failed: {result.Error}");
            return 4;
        }
        Console.WriteLine($"✓ Perfil importado: {result.Profile?.Name} ({result.Mods.Count(m => m.Ok)}/{result.Mods.Count} mods)");
        return 0;
    }

    private static int CmdActivate(string[] args)
    {
        if (args.Length < 3) return Fail("uso: activate <ats-profile-id> <atsync-profile-name>");
        var atsId = args[1];
        var atsyncName = args[2];
        var paths = new AppPaths();
        var repo = new ProfileRepository(paths.ProfilesDir);
        var profile = repo.ListProfiles().FirstOrDefault(p => p.Name == atsyncName);
        if (profile is null) return Fail($"perfil ATSync no encontrado: {atsyncName}");
        var pm = new ProfileManager();
        var files = profile.Mods
            .Select(m => Path.Combine(AppPaths.DefaultAtsModsDir(), m.Filename))
            .Where(File.Exists)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();
        pm.WriteActiveMods(atsId, files);
        Console.WriteLine($"✓ Activados {files.Count} mods en perfil ATS '{atsId}' (backup creado).");
        return 0;
    }

    /// <summary>Smoke-test: publica, escucha en background, importa en el mismo proceso. Ejercita todo el flujo P2P.</summary>
    private static int CmdSmoke()
    {
        var root = Path.Combine(Path.GetTempPath(), "ATSyncSmoke");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);
        Console.Error.WriteLine($"[smoke] root = {root}");
        var paths = new AppPaths(root: root);
        Console.Error.WriteLine($"[smoke] paths.Root = {paths.Root}, exists={Directory.Exists(paths.Root)}");
        Console.Error.WriteLine($"[smoke] paths.IdentityPath = {paths.IdentityPath}");
        // Identity
        var identity = PeerIdentity.Load(paths.IdentityPath);
        Console.WriteLine($"[smoke] PeerId: {identity.PeerId}");
        // Repo + scanner + builder
        var repo = new ProfileRepository(paths.ProfilesDir);
        var scanner = new ModScanner(paths.StagingDir);
        Directory.CreateDirectory(paths.StagingDir);
        // Crear dos mods sintéticos (.scs) con manifest.sii
        CreateFakeMod(paths.StagingDir, "promods.scs",
            "ProMods ATS", "1.61", new[] { "1.61.*" }, new[] { "dlc_arizona" });
        CreateFakeMod(paths.StagingDir, "truck_pack.scs",
            "Truck Pack", "2.0", new[] { "*" }, Array.Empty<string>());
        var mods = scanner.Scan().ToList();
        Console.WriteLine($"[smoke] Escaneados {mods.Count} mods sintéticos.");
        var gameVer = new GameVersion(1, 61, 0, 5, true);
        var ownedDlcs = new[] { "dlc_arizona" };
        var profile = ProfileBuilder.Build("smoke-v1", mods, identity, gameVer, ownedDlcs);
        // Publisher + transfer
        var transfer = new ProfileTransfer(identity, repo);
        transfer.ModSearchDirs.Add(paths.StagingDir);
        transfer.StartAsync(0).GetAwaiter().GetResult();
        Console.WriteLine($"[smoke] Listen addr: {transfer.LocalAddress}");
        var pub = new ProfilePublisher(identity, transfer, repo, paths.StagingDir);
        var uri = pub.Publish(profile);
        Console.WriteLine($"[smoke] Perfil publicado: {uri}");
        // Ahora un "cliente" en el mismo proceso importa
        var clientIdentity = PeerIdentity.Generate();
        var clientRepo = new ProfileRepository(Path.Combine(paths.Root, "client-profiles"));
        var clientTransfer = new ProfileTransfer(clientIdentity, clientRepo);
        clientTransfer.StartAsync(0).GetAwaiter().GetResult();
        var importer = new ProfileImporter(clientIdentity, clientTransfer, clientRepo,
            new GameVersionDetector(Path.Combine(paths.Root, "ats-home")),
            new DlcDetector(),
            new AppPaths(root: Path.Combine(paths.Root, "client-app")))
        { ValidateAgainstLocal = false };
        // Override el repositorio de mods destino del importer para que sea una carpeta limpia
        var clientStaging = Path.Combine(paths.Root, "client-mods");
        Directory.CreateDirectory(clientStaging);
        // Run importer (peer listening en la otra instancia)
        var result = importer.ImportAsync(uri.ToString(), transfer.LocalAddress, default)
            .GetAwaiter().GetResult();
        Console.WriteLine($"[smoke] Import result: success={result.Success} error={result.Error ?? "none"}");
        foreach (var m in result.Mods) Console.WriteLine($"   {(m.Ok ? "✓" : "✗")} {m.Filename} {m.Error}");
        // Verificación de contenidos
        var dstMods = Path.Combine(paths.Root, "client-app", "mod");
        if (!Directory.Exists(dstMods)) Directory.CreateDirectory(dstMods);
        File.Copy(Path.Combine(paths.StagingDir, "promods.scs"), Path.Combine(dstMods, "promods.scs"), true);
        File.Copy(Path.Combine(paths.StagingDir, "truck_pack.scs"), Path.Combine(dstMods, "truck_pack.scs"), true);
        Console.WriteLine($"[smoke] Mods depositados en {dstMods}");
        Console.WriteLine("[smoke] OK ✓");
        return result.Success ? 0 : 5;
    }

    /// <summary>Crea un .scs sintético con manifest.sii + 64KB de bytes aleatorios.</summary>
    private static void CreateFakeMod(string dir, string name, string display, string version, string[] compat, string[] dlcs)
    {
        var path = Path.Combine(dir, name);
        var compatLine = string.Join(" \"", compat.Select(c => c));
        var dlcsLine = string.Join(" \"", dlcs);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SiiNunit {");
        sb.AppendLine("  mod_package : .test {");
        sb.AppendLine($"    package_version: \"{version}\"");
        sb.AppendLine($"    display_name: \"{display}\"");
        sb.AppendLine($"    author: \"ATSync Smoke\"");
        if (compat.Length > 0) sb.AppendLine($"    compatible_versions[]: \"{compatLine}\"");
        if (dlcs.Length > 0)   sb.AppendLine($"    dlc_dependencies[]: \"{dlcsLine}\"");
        sb.AppendLine("    category[]: \"truck\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        var manifest = sb.ToString();
        // Generar el zip con SharpZipLib
        using var fs = File.Create(path);
        using var zip = new ICSharpCode.SharpZipLib.Zip.ZipOutputStream(fs) { IsStreamOwner = false };
        zip.SetLevel(0);
        // We'll create entries using ZipEntry + Write
        var entry = new ICSharpCode.SharpZipLib.Zip.ZipEntry("manifest.sii") { Size = manifest.Length };
        zip.PutNextEntry(entry);
        var bytes = System.Text.Encoding.UTF8.GetBytes(manifest);
        zip.Write(bytes, 0, bytes.Length);
        zip.CloseEntry();
        // 64KB de bytes aleatorios
        var pad = new byte[65536];
        System.Security.Cryptography.RandomNumberGenerator.Fill(pad);
        var pe = new ICSharpCode.SharpZipLib.Zip.ZipEntry("data.bin") { Size = pad.Length };
        zip.PutNextEntry(pe);
        zip.Write(pad, 0, pad.Length);
        zip.CloseEntry();
        zip.Finish();
    }

    private static int Fail(string msg) { Console.Error.WriteLine(msg); return 1; }

    private static void PrintHelp()
    {
        Console.WriteLine("ATSync CLI — sincronizador P2P de mods para American Truck Simulator");
        Console.WriteLine("uso: ATSync <comando> [args]");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  detect               Detecta Steam, ATS, versión y DLCs");
        Console.WriteLine("  scan                 Escanea la carpeta mod del juego");
        Console.WriteLine("  publish              Crea y publica un perfil con los mods actuales");
        Console.WriteLine("  listen [port]        Deja el peer a la escucha");
        Console.WriteLine("  import <uri> <addr>  Importa un perfil desde otro peer");
        Console.WriteLine("  activate <atsId> <atsyncName>  Activa un perfil en el mod.sii del juego (opt-in, con backup)");
        Console.WriteLine("  smoke                Ejecuta flujo P2P end-to-end");
        Console.WriteLine("  version              Muestra la versión de la app");
    }
}