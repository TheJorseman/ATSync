using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ATSync.Core.Models;
using ATSync.Core.Profiles;
using ATSync.Core.Util;

namespace ATSync.Core.P2P;

/// <summary>
/// Transferencia simple P2P sobre TCP local (LAN / directo).
/// El primer miembro abre un TcpListener; el segundo se conecta.
/// Esto cubre LAN y VPNs sin requerir libp2p.
/// Para WAN, usar Libp2pHost (Circuit Relay).
///
/// Protocolo de línea (UTF-8):
///   HELLO &lt;peerId&gt;  → server responde con 220 OK\n
///   GETPROFILE &lt;cid&gt; → server envía tamaño (varint) + JSON + LF
///   GETMOD &lt;cid&gt;     → server envía tamaño (varint) + filename\n + raw bytes
/// </summary>
public sealed class ProfileTransfer : IAsyncDisposable
{
    private TcpListener? _listener;
    private readonly PeerIdentity _identity;
    private readonly ProfileRepository _repo;
    private CancellationTokenSource? _cts;

    /// <summary>Directorios donde buscar mods (en orden de preferencia).</summary>
    public List<string> ModSearchDirs { get; } = new();

    public event EventHandler<TransferEvent>? ProfileReceived;
    public event EventHandler<ModReceivedEvent>? ModReceived;
    public event EventHandler<string>? PeerConnected;
    public event EventHandler<string>? Error;

    public int Port { get; private set; }
    public bool IsRunning => _listener is not null;

    public ProfileTransfer(PeerIdentity identity, ProfileRepository repo)
    {
        _identity = identity;
        _repo = repo;
        ModSearchDirs.Add(Util.AppPaths.DefaultAtsModsDir());
    }

    public async Task StartAsync(int port = 0, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync, _cts.Token);
        await Task.CompletedTask;
    }

    public string LocalAddress => $"tcp://127.0.0.1:{Port}";

    private async Task AcceptLoopAsync()
    {
        var token = _cts!.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClient(client), token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { Error?.Invoke(this, ex.Message); }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        var hello = await reader.ReadLineAsync() ?? "";
        if (!hello.StartsWith("HELLO "))
        {
            await writer.WriteLineAsync("421 ERROR");
            return;
        }
        PeerConnected?.Invoke(this, hello["HELLO ".Length..].Trim());
        await writer.WriteLineAsync("220 OK");

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
            if (line.StartsWith("GETPROFILE "))
            {
                var cid = line["GETPROFILE ".Length..].Trim();
                var txt = FindProfileJsonByCid(cid);
                if (txt is null) { await writer.WriteLineAsync("404"); continue; }
                var bytes = Encoding.UTF8.GetBytes(txt);
                await writer.WriteLineAsync($"200 {bytes.Length}");
                await stream.WriteAsync(bytes);
            }
            else if (line.StartsWith("GETMOD "))
            {
                var cid = line["GETMOD ".Length..].Trim();
                var path = FindModPathByCid(cid);
                if (path is null) { await writer.WriteLineAsync("404"); continue; }
                var fi = new FileInfo(path);
                await writer.WriteLineAsync($"200 {fi.Length} {fi.Name}");
                using var fs = File.OpenRead(path);
                await fs.CopyToAsync(stream);
            }
        }
    }

    private string? FindProfileJsonByCid(string cid)
    {
        foreach (var f in _repo.ListFiles())
        {
            try
            {
                var p = ProfileSerializer.Deserialize(File.ReadAllText(f));
                if (ComputeProfileCidLocal(p) == cid) return File.ReadAllText(f);
            }
            catch { }
        }
        return null;
    }

    private static string ComputeProfileCidLocal(AtsyncProfile p)
    {
        var json = ProfileSerializer.Serialize(p);
        return CidBuilder.ComputeCid(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private string? FindModPathByCid(string cid)
    {
        foreach (var dir in ModSearchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.scs"))
            {
                if (string.IsNullOrEmpty(cid)) return f;
                var realCid = CidBuilder.ComputeCidOfFile(f);
                if (realCid == cid) return f;
            }
        }
        return null;
    }

    /// <summary>Cliente: conecta a otro peer en localhost:p y descarga un perfil.</summary>
    public async Task<AtsyncProfile?> DownloadProfileAsync(string address, string cid, CancellationToken ct = default)
    {
        var uri = address.StartsWith("tcp://") ? address["tcp://".Length..] : address;
        var ix = uri.LastIndexOf(':');
        var host = uri[..ix];
        var port = int.Parse(uri[(ix + 1)..]);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);
        await using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        await writer.WriteLineAsync($"HELLO {_identity.PeerId}");
        var resp = await reader.ReadLineAsync(ct);
        if (resp is null || !resp.StartsWith("220")) throw new InvalidOperationException("peer rejected connection");

        await writer.WriteLineAsync($"GETPROFILE {cid}");
        var status = await reader.ReadLineAsync(ct);
        if (status is null || !status.StartsWith("200 ")) return null;
        var len = int.Parse(status["200 ".Length..]);
        var buf = new byte[len];
        await stream.ReadExactlyAsync(buf, ct);
        var text = Encoding.UTF8.GetString(buf);
        var profile = ProfileSerializer.Deserialize(text);
        ProfileReceived?.Invoke(this, new TransferEvent(profile));
        return profile;
    }

    public async Task<string?> DownloadModAsync(string address, string cid, string destDir, CancellationToken ct = default)
    {
        var uri = address.StartsWith("tcp://") ? address["tcp://".Length..] : address;
        var ix = uri.LastIndexOf(':');
        var host = uri[..ix];
        var port = int.Parse(uri[(ix + 1)..]);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);
        await using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        await writer.WriteLineAsync($"HELLO {_identity.PeerId}");
        var resp = await reader.ReadLineAsync(ct);
        if (resp is null || !resp.StartsWith("220")) throw new InvalidOperationException("peer rejected connection");

        await writer.WriteLineAsync($"GETMOD {cid}");
        var status = await reader.ReadLineAsync(ct);
        if (status is null || !status.StartsWith("200 ")) return null;
        var parts = status["200 ".Length..].Split(' ');
        var size = long.Parse(parts[0]);
        var filename = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : $"{cid}.scs";
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, filename);
        using var fs = File.Create(destPath);
        var remaining = size;
        var pool = new byte[81920];
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(pool.Length, remaining);
            var got = await stream.ReadAsync(pool.AsMemory(0, toRead), ct);
            if (got <= 0) break;
            await fs.WriteAsync(pool.AsMemory(0, got), ct);
            remaining -= got;
        }
        ModReceived?.Invoke(this, new ModReceivedEvent(destPath, cid, size));
        return destPath;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        await Task.CompletedTask;
    }
}

public sealed record TransferEvent(AtsyncProfile Profile);
public sealed record ModReceivedEvent(string LocalPath, string Cid, long Size);