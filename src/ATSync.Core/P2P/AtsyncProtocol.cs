using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethermind.Libp2p.Core;

namespace ATSync.Core.P2P;

/// <summary>
/// Protocolo app-layer ATSync sobre libp2p. Permite a dos pares negociar
/// perfiles y mods sobre una sesión multiplexada (yamux).
///
/// Wire format (line-based, UTF-8):
///   HELLO &lt;AppPeerId&gt;                       ↔ HELLO &lt;AppPeerId&gt;
///   LIST                                    ↔ NAMES count\nname1\nname2\n...\n
///   GETPROFILE &lt;name&gt;                      ↔ SIZE &lt;bytes&gt;\n&lt;json body&gt;
///   GETMOD &lt;hash&gt;                          ↔ MOD &lt;hash&gt; SIZE &lt;bytes&gt;\n&lt;raw bytes&gt;
///   BYE                                      ↔ BYE
///
/// Multiaddr wire id: `/atsync/profile/1.0.0` (debe coincidir en ambos pares).
/// Implementa <see cref="ISessionProtocol"/> directamente para tener control total
/// sobre el flujo dialer/listener; heredar de SymmetricSessionProtocol hace que
/// `DialAsync(IChannel, ISessionContext)`/`ListenAsync(...)` no sean virtuales.
///
/// Operación del dialer (lado cliente): se configura ANTES de
/// <c>await session.DialAsync&lt;AtsyncProtocol&gt;(ct)</c> con <see cref="PendingOp"/>
/// + <see cref="PendingArg"/>. Una vez finalizada la llamada, los bytes recibidos
/// quedan en <see cref="LastDownload"/>.
/// </summary>
public sealed class AtsyncProtocol : ISessionProtocol, ISessionListenerProtocol, IProtocol
{
    public const string ProtocolId = "/atsync/profile/1.0.0";

    public string Id => ProtocolId;

    /// <summary>Directorio donde buscar perfiles para servir en LIST/GETPROFILE.</summary>
    public string ProfilesDir { get; set; } = "";

    /// <summary>Directorio donde buscar mods (.scs) para servir en GETMOD.</summary>
    public string ModsDir { get; set; } = "";

    /// <summary>Identificador de la app ATSync para HELLO (opcional, sólo informativo).</summary>
    public string AppPeerId { get; set; } = "";

    /// <summary>Logging opcional.</summary>
    public ILogger Log { get; set; } = NullLogger.Instance;

    /// <summary>Operación a ejecutar cuando se abra un stream saliente con este protocolo.</summary>
    public DialOperation PendingOp { get; set; } = DialOperation.SmokePing;

    /// <summary>Argumento de la operación (profile CID o mod hash).</summary>
    public string PendingArg { get; set; } = "";

    /// <summary>Resultado del último download completado por el lado dialer.</summary>
    public DownloadResult? LastDownload { get; private set; }

    // Sesiones activas (entrantes) para que el host pueda enumerarlas si lo necesita.
    private static readonly ConcurrentBag<AtsyncProtocol> _instances = new();
    public static IReadOnlyCollection<AtsyncProtocol> ActiveInstances => _instances;

    public AtsyncProtocol()
    {
        _instances.Add(this);
    }

    public enum DialOperation
    {
        /// <summary>HELLO + LIST + BYE (smoke test; no payload).</summary>
        SmokePing,
        /// <summary>HELLO + GETPROFILE &lt;PendingArg&gt; + read + BYE.</summary>
        DownloadProfile,
        /// <summary>HELLO + GETMOD &lt;PendingArg&gt; + read + BYE.</summary>
        DownloadMod,
    }

    public sealed class DownloadResult
    {
        public bool Success { get; init; }
        public byte[]? Payload { get; init; }
        public string? ContentName { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>Lado dialer (cliente): HELLO + [op] + BYE según <see cref="PendingOp"/>.</summary>
    public async Task DialAsync(IChannel channel, ISessionContext context)
    {
        Log.LogInformation("AtsyncProtocol: DialAsync op={Op} canal={Channel}",
            PendingOp, channel.GetHashCode());

        LastDownload = null;
        try
        {
            // HELLO bidireccional.
            await channel.WriteLineAsync($"HELLO {AppPeerId}", prependedWithSize: false).ConfigureAwait(false);
            var helloLine = await channel.ReadLineAsync().ConfigureAwait(false);
            Log.LogDebug("AtsyncProtocol dialer HELLO rx: {Line}", helloLine);

            switch (PendingOp)
            {
                case DialOperation.SmokePing:
                    await RunSmokeAsync(channel).ConfigureAwait(false);
                    break;
                case DialOperation.DownloadProfile:
                    LastDownload = await RunDownloadAsync(channel, "GETPROFILE", PendingArg).ConfigureAwait(false);
                    break;
                case DialOperation.DownloadMod:
                    LastDownload = await RunDownloadAsync(channel, "GETMOD", PendingArg).ConfigureAwait(false);
                    break;
            }

            // BYE.
            await channel.WriteLineAsync("BYE", prependedWithSize: false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "AtsyncProtocol.DialAsync falló");
            LastDownload = new DownloadResult { Success = false, Error = ex.Message };
        }
        finally
        {
            try { await channel.WriteEofAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
        }
    }

    private async Task RunSmokeAsync(IChannel channel)
    {
        await channel.WriteLineAsync("LIST", prependedWithSize: false).ConfigureAwait(false);
        var namesHeader = await channel.ReadLineAsync().ConfigureAwait(false);
        Log.LogInformation("AtsyncProtocol dialer LIST rx: {Line}", namesHeader);
        if (namesHeader != null && namesHeader.StartsWith("NAMES ", StringComparison.Ordinal))
        {
            if (int.TryParse(namesHeader.Substring(6).Trim(), out var count))
            {
                for (int i = 0; i < count; i++)
                {
                    var n = await channel.ReadLineAsync().ConfigureAwait(false);
                    Log.LogDebug("AtsyncProtocol dialer NAME rx: {Line}", n);
                }
            }
        }
    }

    private async Task<DownloadResult> RunDownloadAsync(IChannel channel, string verb, string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return new DownloadResult { Success = false, Error = $"Falta argumento para {verb}" };

        await channel.WriteLineAsync($"{verb} {arg}", prependedWithSize: false).ConfigureAwait(false);
        var header = await channel.ReadLineAsync().ConfigureAwait(false);
        if (header is null)
            return new DownloadResult { Success = false, Error = "Sin respuesta del peer" };

        if (header.StartsWith("ERR ", StringComparison.Ordinal))
            return new DownloadResult { Success = false, Error = header.Substring(4) };

        // Para GETPROFILE: header = "SIZE <bytes>"
        // Para GETMOD:     header = "MOD <hash> SIZE <bytes>"
        int size;
        string contentName = arg;
        if (verb == "GETMOD" && header.StartsWith("MOD ", StringComparison.Ordinal))
        {
            var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || parts[2] != "SIZE") return new DownloadResult { Success = false, Error = $"Header GETMOD malformado: {header}" };
            contentName = parts[1];
            size = int.Parse(parts[3]);
        }
        else if (header.StartsWith("SIZE ", StringComparison.Ordinal))
        {
            size = int.Parse(header.Substring(5));
        }
        else
        {
            return new DownloadResult { Success = false, Error = $"Header inesperado: {header}" };
        }

        // Lee <size> bytes exactos del canal.
        var buf = new byte[size];
        var read = await ReadExactAsync(channel, buf, 0, size).ConfigureAwait(false);
        if (read != size)
            return new DownloadResult { Success = false, Error = $"Read corto: {read}/{size}" };

        return new DownloadResult { Success = true, Payload = buf, ContentName = contentName };
    }

    /// <summary>Lee exactamente <paramref name="count"/> bytes del canal usando ReadAsync.</summary>
    private static async Task<int> ReadExactAsync(IChannel channel, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            var rr = await channel.ReadAsync(count - total, ReadBlockingMode.WaitAll, CancellationToken.None).ConfigureAwait(false);
            if (rr.Result != IOResult.Ok || rr.Data.Length == 0) break;
            rr.Data.CopyTo(new Span<byte>(buffer, offset + total, (int)Math.Min(rr.Data.Length, count - total)));
            total += (int)rr.Data.Length;
        }
        return total;
    }

    /// <summary>Lado listener (servidor): sirve HELLO y comandos hasta BYE.</summary>
    public async Task ListenAsync(IChannel channel, ISessionContext context)
    {
        Log.LogInformation("AtsyncProtocol: ListenAsync (server) en canal {Channel} desde {Remote}",
            channel.GetHashCode(), context.Id);

        try
        {
            // Enviar HELLO primero (el par remoto también debería enviar uno).
            await channel.WriteLineAsync($"HELLO {AppPeerId}", prependedWithSize: false).ConfigureAwait(false);

            while (!channel.CancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await channel.ReadLineAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.LogDebug(ex, "ReadLine falló (cerrado)");
                    break;
                }

                if (string.IsNullOrEmpty(line)) break;
                var trimmed = line.Trim();
                Log.LogDebug("AtsyncProtocol server rx: {Line}", trimmed);

                if (trimmed.StartsWith("HELLO ", StringComparison.Ordinal)) continue;
                if (string.Equals(trimmed, "BYE", StringComparison.Ordinal))
                {
                    await channel.WriteLineAsync("BYE", prependedWithSize: false).ConfigureAwait(false);
                    break;
                }
                if (string.Equals(trimmed, "LIST", StringComparison.Ordinal))
                {
                    await HandleListAsync(channel).ConfigureAwait(false);
                    continue;
                }
                if (trimmed.StartsWith("GETPROFILE ", StringComparison.Ordinal))
                {
                    var name = trimmed.Substring("GETPROFILE ".Length).Trim();
                    await HandleGetProfileAsync(channel, name).ConfigureAwait(false);
                    continue;
                }
                if (trimmed.StartsWith("GETMOD ", StringComparison.Ordinal))
                {
                    var hash = trimmed.Substring("GETMOD ".Length).Trim();
                    await HandleGetModAsync(channel, hash).ConfigureAwait(false);
                    continue;
                }
                await channel.WriteLineAsync("ERR unknown-command", prependedWithSize: false).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "AtsyncProtocol.ListenAsync falló");
        }
        finally
        {
            try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
        }
    }

    private async Task HandleListAsync(IChannel channel)
    {
        IReadOnlyList<string> names;
        try
        {
            names = string.IsNullOrEmpty(ProfilesDir) || !Directory.Exists(ProfilesDir)
                ? Array.Empty<string>()
                : Directory.EnumerateFiles(ProfilesDir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => n != null)
                    .Select(n => n!)
                    .ToArray();
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "List falló");
            names = Array.Empty<string>();
        }
        await channel.WriteLineAsync($"NAMES {names.Count}", prependedWithSize: false).ConfigureAwait(false);
        foreach (var n in names)
            await channel.WriteLineAsync(n, prependedWithSize: false).ConfigureAwait(false);
    }

    private async Task HandleGetProfileAsync(IChannel channel, string name)
    {
        if (string.IsNullOrEmpty(ProfilesDir)) { await SendErr(channel, "no-profiles-dir"); return; }
        var path = Path.Combine(ProfilesDir, name + ".json");
        if (!File.Exists(path)) { await SendErr(channel, "not-found"); return; }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            await channel.WriteLineAsync($"SIZE {bytes.Length}", prependedWithSize: false).ConfigureAwait(false);
            await channel.WriteAsync(new ReadOnlySequence<byte>(bytes), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "GetProfile falló");
            await SendErr(channel, "io-error");
        }
    }

    private async Task HandleGetModAsync(IChannel channel, string hash)
    {
        if (string.IsNullOrEmpty(ModsDir)) { await SendErr(channel, "no-mods-dir"); return; }
        var path = Path.Combine(ModsDir, hash + ".scs");
        if (!File.Exists(path)) { await SendErr(channel, "not-found"); return; }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            await channel.WriteLineAsync($"MOD {hash} SIZE {bytes.Length}", prependedWithSize: false).ConfigureAwait(false);
            await channel.WriteAsync(new ReadOnlySequence<byte>(bytes), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "GetMod falló");
            await SendErr(channel, "io-error");
        }
    }

    private static async Task SendErr(IChannel channel, string code)
    {
        await channel.WriteLineAsync($"ERR {code}", prependedWithSize: false).ConfigureAwait(false);
    }
}