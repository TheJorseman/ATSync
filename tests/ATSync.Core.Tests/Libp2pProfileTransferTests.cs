using ATSync.Core.P2P;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ATSync.Core.Tests;

/// <summary>
/// E2E: dos <see cref="Libp2pHost"/> en el mismo proceso, uno sirve un perfil
/// ATSync y el otro lo descarga vía <see cref="Libp2pHost.DownloadProfileAsync"/>.
///
/// Nota: cada host tiene su propio <c>ServiceProvider</c> + <c>IPeerFactory</c>,
/// por lo que NO comparten stacks libp2p. Esto replica exactamente el flujo
/// entre dos instancias de la app.
/// </summary>
public class Libp2pProfileTransferTests
{
    [Fact(Timeout = 60_000)]
    public async Task TwoLocalPeersExchangeProfile()
    {
        // Logger silencioso para no inundar la salida del test.
        var logA = NullLogger<Libp2pHost>.Instance;
        var logB = NullLogger<Libp2pHost>.Instance;

        var identityA = PeerIdentity.Generate();
        var identityB = PeerIdentity.Generate();

        var hostA = new Libp2pHost(identityA, logA);
        var hostB = new Libp2pHost(identityB, logB);

        // Prepara un perfil de prueba en el directorio del host B.
        var profilesDir = Path.Combine(Path.GetTempPath(), "atsync-libp2p-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(profilesDir);
        var profileJson = """{"name":"test-profile","author":"hostB","mods":[]}""";
        var profileFile = Path.Combine(profilesDir, "test-profile.json");
        await File.WriteAllTextAsync(profileFile, profileJson);
        hostB.AppProtocol.ProfilesDir = profilesDir;

        try
        {
            // Arranca ambos hosts en puertos aleatorios (0).
            await hostA.StartAsync(new[] { "/ip4/127.0.0.1/tcp/0" }, enableRelay: false);
            await hostB.StartAsync(new[] { "/ip4/127.0.0.1/tcp/0" }, enableRelay: false);

            Assert.True(hostA.IsRunning);
            Assert.True(hostB.IsRunning);
            Assert.NotEmpty(hostA.ListenMultiaddrs);
            Assert.NotEmpty(hostB.ListenMultiaddrs);

            // hostA dials hostB.
            var bAddr = hostB.ListenMultiaddrs.First();

            // El método se llama desde el lado caller: DownloadProfileAsync dialea
            // a bAddr, abre stream /atsync/profile/1.0.0 con PendingOp=DownloadProfile,
            // recibe SIZE + JSON bytes y devuelve DownloadResult.
            var dl = await hostA.DownloadProfileAsync(bAddr, "test-profile");

            Assert.True(dl.Success, $"Download falló: {dl.Error}");
            Assert.NotNull(dl.Payload);
            Assert.Equal(profileJson.Length, dl.Payload!.Length);
            var received = System.Text.Encoding.UTF8.GetString(dl.Payload);
            Assert.Equal(profileJson, received);
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
            try { Directory.Delete(profilesDir, recursive: true); } catch { }
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task SmokeHelloListByeRoundtrip()
    {
        // Verifica el flujo "smoke" original: HELLO + LIST + BYE con un perfil
        // de prueba en B. El resultado queda en AtsyncProtocol.LastDownload
        // sólo si se hizo Download; para el smoke, simplemente validamos que
        // la sesión cierra limpia (DownloadProfileAsync con PendingOp por defecto
        // SmokePing no produce payload pero DialAsync retorna sin excepción).

        var log = NullLogger<Libp2pHost>.Instance;
        var identityA = PeerIdentity.Generate();
        var identityB = PeerIdentity.Generate();

        var hostA = new Libp2pHost(identityA, log);
        var hostB = new Libp2pHost(identityB, log);

        var profilesDir = Path.Combine(Path.GetTempPath(), "atsync-smoke-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(profilesDir);
        await File.WriteAllTextAsync(Path.Combine(profilesDir, "demo.json"), """{"name":"demo"}""");
        hostB.AppProtocol.ProfilesDir = profilesDir;

        try
        {
            await hostA.StartAsync(new[] { "/ip4/127.0.0.1/tcp/0" }, enableRelay: false);
            await hostB.StartAsync(new[] { "/ip4/127.0.0.1/tcp/0" }, enableRelay: false);

            // Reset to smoke default (no payload).
            hostA.AppProtocol.PendingOp = AtsyncProtocol.DialOperation.SmokePing;
            hostA.AppProtocol.PendingArg = "";

            var bAddr = hostB.ListenMultiaddrs.First();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Dial directo sin DownloadProfileAsync: usamos session.DialAsync.
            var session = await hostA.DialAsync(bAddr, cts.Token);
            try
            {
                await session.DialAsync<AtsyncProtocol>(cts.Token);
                // SmokePing no produce payload, pero DialAsync retorna tras HELLO+LIST+BYE.
            }
            finally
            {
                try { await session.DisconnectAsync(); } catch { }
            }
        }
        finally
        {
            await hostA.StopAsync();
            await hostB.StopAsync();
            try { Directory.Delete(profilesDir, recursive: true); } catch { }
        }
    }
}