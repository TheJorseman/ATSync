# ATSync

Sincronizador **P2P** de mods para *American Truck Simulator* (ATS). Permite a grupos de amigos compartir y mantener **perfiles idénticos de mods** sin servidor central.

> Estado: v0.2.0 — UI + CLI + libp2p integrado (Relay v2 + DCUtR ready).

La UI Avalonia 11 tiene 6 pestañas (Detección / Mods / Perfiles / Importar / Escuchar / Ajustes) con tema oscuro, accent turquesa, validación inline por mod y status bar dinámica. Captura pendiente de generar — ver `scripts/capture_screenshot.ps1` para reproducirla.

## Características

- **Detección automática** del juego, su versión (`game.log.txt`) y los DLCs instalados (manifests de Steam).
- **Escaneo y validación** de mods locales: parsea el `manifest.sii` de cada `.scs`, verifica `compatible_versions[]` y `dlc_dependencies[]`.
- **Perfiles ATSync** con orden de prioridad, requisitos de juego/DLCs y verificación SHA-256.
- **Identidad P2P** Ed25519 por usuario, PeerID `libp2p` (`12D3KooW…`) almacenada cifrada con DPAPI en Windows.
- **Transferencia peer-to-peer** sobre TCP con un protocolo de línea minimal (HELLO/GETPROFILE/GETMOD). Funciona en LAN sin abrir puertos manualmente.
- **Activación opt-in** de mods en el perfil de ATS (`mod.sii` rewrite + backup `mod.sii.bak`).
- **CIDs** content-addressing estilo `bafkrei…` para identificar mods de forma robusta.
- **CLI** lista para ejecutar y probar todos los flujos (`detect`, `scan`, `publish`, `listen`, `import`, `activate`, `smoke`, `version`).
- **Tests** xUnit con 26 tests verdes (parser, validador, CID, serializer, identidad, versionado).

## Roadmap

| Estado | Componente |
|---|---|
| ✅ v0.1 | Detección ATS, parser SiiNunit, perfil ATSync, CID, PeerIdentity, transfer TCP |
| ✅ v0.1.x | Interfaz gráfica (Avalonia 11, 6 pestañas, tema oscuro, validación inline) |
| ✅ v0.2 | Integración con `Nethermind.Libp2p 1.0.1` (Relay v2 + DCUtR ready, E2E smoke OK) |
| ⏳ v0.3 | UI: arrancar Libp2pHost en background, importar via multiaddr libp2p, peer discovery via Kad-DHT |
| ⏳ v0.4 | Perfiles privados (PSK Noise + Ed25519 real) |
| ⏳ v0.5 | System tray + auto-update + instalador NSIS |

## Quickstart

```bash
# Compilar
dotnet build

# Detectar ATS, versión y DLCs en el PC
dotnet run --project src/ATSync.Cli -- detect

# Escanear mods instalados
dotnet run --project src/ATSync.Cli -- scan

# Flujo end-to-end (sin ATS, en carpeta temp)
dotnet run --project src/ATSync.Cli -- smoke

# Publicar perfil a partir de tus mods actuales
dotnet run --project src/ATSync.Cli -- publish

# Activar un perfil ATSync en un perfil ATS existente (con backup)
dotnet run --project src/ATSync.Cli -- activate <ats-profile-id> <atsync-profile-name>

# Escuchar para que importen tus mods (LAN)
dotnet run --project src/ATSync.Cli -- listen
```

## Estructura

```
src/
  ATSync.Core/         # lógica (parser, scanner, validador, perfil, P2P)
  ATSync.Cli/          # ejecutable consola (smoke tests, usuario avanzado)
tests/
  ATSync.Core.Tests/   # xUnit, 26 tests
docs/                  # documentación adicional
```

## Compatibilidad

- **Juego**: American Truck Simulator 1.61 (estable, sept 2026) + 1.60 (anterior). El parser de versiones usa wildcards (`1.61.*`, `1.6*.*`).
- **DLCs**: tabla bundled con los 45 DLCs conocidos a 2026-10-03 (Arizona, todos los estados hasta South Dakota + Road Trip: Ford + cargo/tuning/paint/trucks).
- **OS**: Windows (target principal). Linux/macOS compilan pero el cifrado DPAPI y el Win Registry se degradan.
- **.NET**: .NET 10 SDK. (Se instaló con `dotnet-install.ps1`).

## Licencia

GPL-3.0. Ver `LICENSE`.

Dependencias (todas compatibles GPL-3.0):

| Paquete | Versión | Licencia |
|---|---|---|
| SharpZipLib | 1.3.3 | MIT (con excepción GPL) |
| Microsoft.Extensions.Logging | 10.0.x | MIT |
| System.Security.Cryptography.ProtectedData | 9.0.0 | MIT |

`Nethermind.Libp2p` 1.0.1 (MIT) integrado en v0.2 — ver sección siguiente.

## Integración libp2p (v0.2)

ATSync v0.2 integra `Nethermind.Libp2p 1.0.1` como segundo transporte P2P junto al `ProfileTransfer` TCP. La capa libp2p se compone con `WithRelay()` (Circuit Relay v2 + DCUtR hole-punching) sobre un stack TCP + Noise + Yamux + Multistream-Select.

Componentes nuevos en `ATSync.Core.P2P`:

- **`Libp2pHost`** — genera identidad Ed25519 (`Identity()`), abre el listener TCP, registra el protocolo app-layer y expone `Libp2pPeerId` (`12D3KooW…`) y `ListenMultiaddrs` (`/ip4/.../tcp/.../p2p/12D3KooW…`).
- **`AtsyncProtocol`** (`ISessionProtocol`) — protocolo app-layer `/atsync/profile/1.0.0` que corre dentro de la sesión multiplexada. Implementa HELLO/LIST/GETPROFILE/GETMOD/BYE sobre `IChannel.WriteLineAsync`/`ReadLineAsync`. Se registra vía `IPeerFactoryBuilder.AddProtocol(AppProtocol, isExposed: true)`.

E2E smoke test verificado (`C:\Users\migue\AppData\Local\Temp\libp2p-smoke`):

- Host A y Host B arrancan con Ed25519 + Relay on.
- Negociación TCP + Noise + Yamux + Multistream + Identify OK.
- `/atsync/profile/1.0.0` aparece en el `signedPeerRecord` de Identify (`"protocols": ["/ipfs/id/1.0.0", "/ipfs/id/push/1.0.0", "/ipfs/ping/1.0.0", "/atsync/profile/1.0.0"]`).
- `session.DialAsync<AtsyncProtocol>(ct)` completa con `RanToCompletion` tras HELLO + LIST + BYE.

Wire format del protocolo (UTF-8, line-based sobre `IChannel`):

```
HELLO <appPeerId>
LIST                    -> NAMES <count>\n<name1>\n<name2>\n...
GETPROFILE <name>       -> SIZE <bytes>\n<json body>
GETMOD <hash>           -> MOD <hash> SIZE <bytes>\n<raw bytes>
BYE                     -> BYE
```

NAT traversal: dos pares detrás de NAT pueden negociar canales directos vía Relay v2 (HOP) y luego DCUtR perfora el NAT. Configurable en `Ajustes > Transporte P2P > Habilitar Circuit Relay v2`.

## NAT traversal — qué hay y qué falta (honesto)

**Con `WithQuic()` + `WithRelay()` + mDNS, ATSync v0.2 ofrece:**

| Escenario | Soporte | Notas |
|---|---|---|
| Misma LAN (mismo router WiFi) | ✅ automático | mDNS descubre el peer sin config |
| Uno con IP pública, otro en NAT | ✅ manual | El peer público abre puerto; el otro dialea |
| Ambos en NAT, mismo CGNAT grande | ⚠️ necesita relay | Por defecto NO hay relay configurado |
| NAT simétrico / CGNAT duro | ❌ | Igual que BitTorrent: requiere VPN (Tailscale/ZeroTier) o relay público |

**Qué tenemos y qué no en Nethermind.Libp2p 1.0.1 (v0.2):**

- ✅ Relay v2 wire protocol (`WithRelay()`): el peer SABE usar relay, pero no descubre relays automáticamente.
- ✅ mDNS discovery (LAN, automático).
- ✅ QUIC transport (`WithQuic()`, habilitado por defecto en v0.2): UDP-based, atraviesa NATs más fácilmente que TCP y soporta connection migration.
- ❌ AutoRelay client mode: no expuesto en la API estable. Imposible descubrir relays vía DHT sin upstream changes.
- ❌ DCUtR hole-punching: no expuesto en la API estable. Una vez conectados vía relay, no hay upgrade automático a conexión directa.
- ❌ Configuración de relay server URLs: no hay método público para apuntar a relays conocidos.

**Mientras tanto, opciones prácticas (sin esperar a upstream):**

1. **misma LAN** — cero config, automático (mDNS).

2. **Tailscale / ZeroTier / Hamachi** — la solución que BitTorrent también usa cuando los NATs son hostiles. Instala una VPN mesh entre los dos PCs; el transporte libp2p ve una IP "pública" virtual y funciona sin más.

3. **Relay público manual** — apunta a uno conocido vía multiaddr compuesto `/ip4/<host>/tcp/<port>/p2p/<relay-id>/p2p/<target-id>`. Esto es experimental en v0.2 (no testeado contra relays públicos desde este repo).

4. **Hospeda tu propio relay** — Nethermind.Libp2p 1.0.1 expone el protocolo Relay v2 server (`RelayHopProtocol`), por lo que un peer ATSync puede actuar como relay para otros. Necesita IP pública o VPS; fuera del scope de este repo por ahora.

**Por qué BitTorrent "no tiene NAT"** (matices importantes):
- BT tiene 20+ años de infraestructura global: routers DHT públicos hardcoded (`router.bittorrent.com:6881`) y millones de seeds.
- BT usa uTP (UDP) — mismo principio que QUIC. ATSync ya lo habilita.
- BT no soluciona CGNAT simétrico: en ese caso el usuario también usa VPN/seedbox.

Esto NO es una limitación específica de ATSync — es la realidad del peer-to-peer contra NATs restrictivos. La diferencia es que BitTorrent tiene más infraestructura para mitigar el problema.

Configuración NAT en `Ajustes`:
- `Habilitar Circuit Relay v2` — activa el wire protocol (default ON).
- `Habilitar QUIC` — activa transporte UDP (default ON, v0.2+).
- El protocolo actual es simétrico y stateful; todavía no hay PSK ni autenticación mutua fuerte. La verificación Ed25519 se hace en libp2p a nivel de transporte (Noise), no a nivel aplicación.

## UI (v0.1.0)

`ATSync.App` añade una GUI Avalonia 11 sobre `ATSync.Core`. 6 pestañas:

1. **Detección** — Steam, ATS install, versión detectada, DLCs poseídos (X / 45) agrupados por categoría (Mapas / Carga / Tuning / Camiones / Pintura / Road Trip) y tu PeerID (copiable al portapapeles).
2. **Mods** — lista de `.scs` de la carpeta `mod` de ATS, con filtro de búsqueda, badges de validación por mod (🟢 compatible / 🟡 falta DLC / 🔴 versión incompatible), contador de seleccionados y botón "Publish profile".
4. **Perfiles** — perfiles ATSync guardados localmente en cards (autor, mod count, tamaño total, versión de juego).
5. **Importar** — pega un URI `atsync://profile/…` + la dirección `tcp://…` de tu amigo. Validación inline del URI, descarga, opt-in para activar en perfil ATS con backup.
6. **Escuchar** — botón "Iniciar" para dejar tu peer a la escucha y mostrar `tcp://IP:puerto/` que compartes con amigos. Contador de peers conectados en tiempo real.
7. **Ajustes** — activación automática opt-in, puerto preferido, transporte P2P (`TCP` o `libp2p`), puerto libp2p (default 4001), relay habilitado, perfiles privados (v0.4).

Status bar dinámica refleja eventos P2P en tiempo real (peer conectado, mod recibido, error). Tema oscuro con accent turquesa ATS.

Ejecutable: `dist/app/ATSync.App.exe` (~140 MB self-contained single-file, .NET 10 + libp2p nativo embebido) y `dist/cli/ATSync.Cli.exe` (~116 MB).

```bash
dotnet run --project src/ATSync.App
# o:
dist\ATSync.App.exe
```

Build:

```bash
dotnet publish src/ATSync.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Captura:

```
+--------------------------------------------------------------+
| ATSync v0.1.0 BETA    Sincronizador P2P de mods para ATS  [Copy]|
+--------------------------------------------------------------+
| [1. Detección] [2. Mods] [3. Perfiles] [4. Importar] ...        |
| ...                                                            |
| ATSync v0.1.0 · GPL-3.0                                      |
+--------------------------------------------------------------+
```

## Quickstart

```bash
# Compilar todo
dotnet build

# Lanzar la UI
dist\ATSync.App.exe
# o desde código:
dotnet run --project src\ATSync.App

# CLI (mismos flujos desde terminal):
dotnet run --project src\ATSync.Cli -- detect
dotnet run --project src\ATSync.Cli -- scan
dotnet run --project src\ATSync.Cli -- publish
dotnet run --project src\ATSync.Cli -- listen
dotnet run --project src\ATSync.Cli -- import <atsync-uri> <peer-addr>
dotnet run --project src\ATSync.Cli -- smoke
```