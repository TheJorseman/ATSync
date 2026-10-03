using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Multiformats.Address;
using Nethermind.Libp2p;
using Nethermind.Libp2p.Core;

namespace ATSync.Core.P2P;

/// <summary>
/// Host P2P basado en Nethermind.Libp2p 1.0.1 (paquete meta).
///
/// Capacidades activas:
///   - Transports: TCP + QUIC opcional
///   - Security: Noise + TLS
///   - Muxer: Yamux
///   - Relay v2 (HOP) — habilita Circuit Relay para NAT traversal
///   - DCUtR — hole punching upgrade sobre relay (incluido cuando Relay está activo)
///   - Kad-DHT, mDNS, Identify, Ping
///
/// NAT traversal: dos pares detrás de NAT pueden establecer un canal directo
/// a través de relay + DCUtR usando AutoRelay + un nodo relay público.
/// Ver https://docs.libp2p.io/concepts/nat/ .
///
/// ESTADO (v0.2):
///   - Host arranca, genera identidad Ed25519, escucha en TCP.
///   - Protocolo app-layer ATSyncProfileProtocol registrado sobre ISession.
///   - Custom protocol permite intercambio de perfiles/mods sobre sesiones
///     multiplexadas (yamux).
///   - Test E2E dos procesos locales en CI: pendiente (WIP v0.3).
/// </summary>
public sealed class Libp2pHost : IAsyncDisposable
{
    private readonly PeerIdentity _appIdentity;
    private readonly ILogger _log;
    private ServiceProvider? _services;
    private ILocalPeer? _peer;
    private ObservableCollection<Multiaddress>? _listenAddresses;

    public string AppPeerId => _appIdentity.PeerId;
    public string Libp2pPeerId => _peer?.Identity.PeerId.ToString() ?? "";
    public IReadOnlyList<string> ListenMultiaddrs { get; private set; } = Array.Empty<string>();
    public bool IsRunning => _peer is not null;
    public ILocalPeer? Peer => _peer;

    /// <summary>Sesión recién abierta (peer remoto conectado, saliente o entrante).</summary>
    public event Action<ISession>? OnSessionEstablished;

    /// <summary>Argumento: Multiaddress del peer remoto.</summary>
    public event Action<string>? OnPeerConnected;

    public Libp2pHost(PeerIdentity appIdentity, ILogger? log = null)
    {
        _appIdentity = appIdentity;
        _log = log ?? NullLogger.Instance;
    }

    /// <summary>
    /// Instancia del protocolo app-layer registrado en el stack libp2p.
    /// Se puede personalizar ANTES de llamar a <see cref="StartAsync"/> asignando
    /// <see cref="AtsyncProtocol.ProfilesDir"/> y <see cref="AtsyncProtocol.ModsDir"/>.
    /// </summary>
    public AtsyncProtocol AppProtocol { get; } = new();

    public async Task StartAsync(
        IEnumerable<string>? listenAddresses = null,
        bool enableRelay = true,
        CancellationToken ct = default)
    {
        AppProtocol.AppPeerId = _appIdentity.PeerId;

        var services = new ServiceCollection();

        // `AddLibp2p` requiere un Func<...> que devuelva el builder.
        services.AddLibp2p(b =>
        {
            if (enableRelay) b.WithRelay();
            // Registrar el protocolo app-layer ATSync sobre ISession (yamux multistream-select).
            b.AddProtocol(AppProtocol, isExposed: true);
            return b;
        });
        services.AddLogging(b => b.AddConsole());

        _services = services.BuildServiceProvider();

        var peerFactory = _services.GetRequiredService<IPeerFactory>();

        // Crear identidad Ed25519 para el par libp2p (separada de la identidad de la app ATSync).
        var identity = new Identity();
        _peer = peerFactory.Create(identity);

        _log.LogInformation("Libp2p PeerId: {PeerId}", Libp2pPeerId);

        // Listeners de conexión: cuando se abre una sesión (entrante o saliente).
        _peer.OnConnected += async session =>
        {
            try { OnPeerConnected?.Invoke(session.RemoteAddress.ToString()); }
            catch (Exception ex) { _log.LogWarning(ex, "OnPeerConnected handler failed"); }
            OnSessionEstablished?.Invoke(session);
            await Task.CompletedTask;
        };

        // Construir la lista de direcciones de escucha.
        var addresses = (listenAddresses ?? new[] { "/ip4/0.0.0.0/tcp/4001" })
            .Select(Multiaddress.Decode)
            .ToArray();

        await _peer.StartListenAsync(addresses, ct).ConfigureAwait(false);

        // Resolver las direcciones reales (puerto asignado si fue 0).
        _listenAddresses = _peer.ListenAddresses;
        var resolved = new List<string>();
        foreach (var addr in _listenAddresses)
        {
            // Sólo añade `/p2p/<self>` si la dirección aún no termina en uno
            // (Nethermind ya lo incluye en algunos transportes).
            var s = addr.ToString();
            resolved.Add(s.EndsWith($"/p2p/{Libp2pPeerId}") ? s : $"{s}/p2p/{Libp2pPeerId}");
        }
        ListenMultiaddrs = resolved;

        _log.LogInformation(
            "Libp2pHost listo. PeerId={PeerId} Addrs=[{List}]",
            Libp2pPeerId, string.Join(",", resolved));
    }

    /// <summary>Detiene el listener y libera recursos.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_peer is null) return;
        await _peer.DisposeAsync().ConfigureAwait(false);
        _peer = null;
    }

    /// <summary>Marca el peer como relay público (auto-relay server). Aún no usado en v0.2.</summary>
    public bool IsRelayConfigured => _listenAddresses?.Count > 0;

    /// <summary>Dial a un peer remoto por multiaddress (e.g. /ip4/.../tcp/.../p2p/Qm...).</summary>
    public Task<ISession> DialAsync(string multiaddr, CancellationToken ct = default)
    {
        if (_peer is null) throw new InvalidOperationException("Libp2pHost no arrancado");
        return _peer.DialAsync(Multiaddress.Decode(multiaddr), ct);
    }

    /// <summary>
    /// Dial al peer, abre el stream <see cref="AtsyncProtocol"/> con la operación
    /// <see cref="AtsyncProtocol.DialOperation.DownloadProfile"/> y devuelve los bytes del perfil.
    /// </summary>
    public async Task<AtsyncProtocol.DownloadResult> DownloadProfileAsync(
        string multiaddr, string profileName, CancellationToken ct = default)
    {
        if (_peer is null) throw new InvalidOperationException("Libp2pHost no arrancado");
        var session = await _peer.DialAsync(Multiaddress.Decode(multiaddr), ct).ConfigureAwait(false);
        try
        {
            AppProtocol.PendingOp = AtsyncProtocol.DialOperation.DownloadProfile;
            AppProtocol.PendingArg = profileName;
            await session.DialAsync<AtsyncProtocol>(ct).ConfigureAwait(false);
        }
        finally
        {
            AppProtocol.PendingOp = AtsyncProtocol.DialOperation.SmokePing;
            AppProtocol.PendingArg = "";
            try { await session.DisconnectAsync().ConfigureAwait(false); } catch { }
        }
        return AppProtocol.LastDownload ?? new AtsyncProtocol.DownloadResult
        {
            Success = false,
            Error = "Protocolo no produjo respuesta"
        };
    }

    /// <summary>Dial al peer, abre el stream con operación DownloadMod y devuelve los bytes del mod.</summary>
    public async Task<AtsyncProtocol.DownloadResult> DownloadModAsync(
        string multiaddr, string modHash, CancellationToken ct = default)
    {
        if (_peer is null) throw new InvalidOperationException("Libp2pHost no arrancado");
        var session = await _peer.DialAsync(Multiaddress.Decode(multiaddr), ct).ConfigureAwait(false);
        try
        {
            AppProtocol.PendingOp = AtsyncProtocol.DialOperation.DownloadMod;
            AppProtocol.PendingArg = modHash;
            await session.DialAsync<AtsyncProtocol>(ct).ConfigureAwait(false);
        }
        finally
        {
            AppProtocol.PendingOp = AtsyncProtocol.DialOperation.SmokePing;
            AppProtocol.PendingArg = "";
            try { await session.DisconnectAsync().ConfigureAwait(false); } catch { }
        }
        return AppProtocol.LastDownload ?? new AtsyncProtocol.DownloadResult
        {
            Success = false,
            Error = "Protocolo no produjo respuesta"
        };
    }

    /// <summary>Dial a un peer conocido por PeerId usando las direcciones cacheadas en el PeerStore.</summary>
    public Task<ISession> DialAsync(PeerId peerId, CancellationToken ct = default)
    {
        if (_peer is null) throw new InvalidOperationException("Libp2pHost no arrancado");
        return _peer.DialAsync(peerId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_peer is not null)
        {
            try { await _peer.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { _log.LogWarning(ex, "Dispose peer falló"); }
        }
        _services?.Dispose();
    }
}