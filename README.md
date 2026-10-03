# ATSync

Sincronizador **P2P** de mods para *American Truck Simulator* (ATS). Permite a grupos de amigos compartir y mantener **perfiles idénticos de mods** sin servidor central.

> Estado: v0.1.0 — UI + CLI funcional, probado end-to-end en local.

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
| ⏳ v0.2 | Interfaz gráfica (Avalonia 11) |
| ⏳ v0.3 | Integración con `Nethermind.Libp2p` (Circuit Relay v2 + DCUtR para NAT traversal real sin relay manual) |
| ⏳ v0.4 | Perfiles privados (PSK Noise) |
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

`Nethermind.Libp2p` (MIT) se integrará en v0.3 para NAT traversal sin abrir puertos.

## Estado de la integración libp2p

La capa `Nethermind.Libp2p 1.0.1` requiere .NET 10 (ya disponible en este repo). Sin embargo, su API `ILibp2pPeerFactoryBuilder` no expone los símbolos que asumimos (`AddAppLayerProtocol`, `IHost`, `OnDisconnected`). Para v0.1 usamos un transporte TCP propio (`ProfileTransfer`) que cumple el mismo objetivo en LAN. La capa de adaptación para libp2p está documentada para v0.3 — `ATSync.P2P/ProfileTransfer` será reemplazado por un `Libp2pHost` con Circuit Relay v2 + DCUtR.

## UI (v0.1.0)

`ATSync.App` añade una GUI Avalonia 11 sobre `ATSync.Core`. 6 pestañas:

1. **Detección** — Steam, ATS install, versión detectada, DLCs poseídos (X / 45) agrupados por categoría (Mapas / Carga / Tuning / Camiones / Pintura / Road Trip) y tu PeerID (copiable al portapapeles).
2. **Mods** — lista de `.scs` de la carpeta `mod` de ATS, con filtro de búsqueda, badges de validación por mod (🟢 compatible / 🟡 falta DLC / 🔴 versión incompatible), contador de seleccionados y botón "Publish profile".
4. **Perfiles** — perfiles ATSync guardados localmente en cards (autor, mod count, tamaño total, versión de juego).
5. **Importar** — pega un URI `atsync://profile/…` + la dirección `tcp://…` de tu amigo. Validación inline del URI, descarga, opt-in para activar en perfil ATS con backup.
6. **Escuchar** — botón "Iniciar" para dejar tu peer a la escucha y mostrar `tcp://IP:puerto/` que compartes con amigos. Contador de peers conectados en tiempo real.
7. **Ajustes** — activación automática opt-in, puerto preferido, perfiles privados (v0.4).

Status bar dinámica refleja eventos P2P en tiempo real (peer conectado, mod recibido, error). Tema oscuro con accent turquesa ATS.

Ejecutable: `dist/ATSync.App.exe` (~80 MB, self-contained single-file, .NET 10 embebido).

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