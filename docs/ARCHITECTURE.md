# ATSync — Arquitectura

## Capas

```
┌────────────────────────────────────────────────────────┐
│  ATSync.Cli / ATSync.App (UI)                          │
│    - comandos CLI, Avalonia views (futuro)             │
├────────────────────────────────────────────────────────┤
│  ATSync.Core                                           │
│                                                       │
│   Profiles (AtsyncProfile, ProfileRepository, Builder) │
│   Mods (SiiManifestParser, ScsArchive, ModScanner,    │
│         ModValidator)                                 │
│   Ats (SteamPathLocator, GameVersionDetector,         │
│        DlcDetector, ProfileManager)                   │
│   P2P (PeerIdentity, ProfileUri, CidBuilder,          │
│        ProfileTransfer, ProfilePublisher,             │
│        ProfileImporter)                               │
│   Util (AppPaths, Hashing)                            │
└────────────────────────────────────────────────────────┘
```

## Pipeline de uso

```
User selecciona mods
        ↓
ProfileBuilder.Build()  →  AtsyncProfile
        ↓
ProfileRepository.Save()  →  %LOCALAPPDATA%\ATSync\profiles\<name>.atsync
        ↓
ProfilePublisher.Publish()  →  ProfileUri (atsync://profile/<cid>)
        ↓
[ usuario pega URI en Discord ]

[ peer importador ]
ProfileImporter.ImportAsync(uri, peerAddr)  →
    - ProfileTransfer.DownloadProfileAsync()  →  HANDSHAKE + GETPROFILE
    - por cada mod: ProfileTransfer.DownloadModAsync()  →  HANDSHAKE + GETMOD
    - valida SHA-256, version del juego, DLCs poseídos
    - mueve a Documents\American Truck Simulator\mod/
    - opt-in: ProfileManager.WriteActiveMods()  →  backup mod.sii.bak + nuevo mod.sii
```

## Decisiones técnicas

### PeerID libp2p (no Ed25519 real en v0.1)

- En v0.1 usamos **HMAC-SHA256(seed, "ed25519-v1")** como derivado de "clave pública".
- Razón: .NET 10 SDK se distribuye sin `System.Security.Cryptography.Ed25519` accesible en todas las plataformas de forma estable.
- En v0.3 sustituiremos por el API completo de Nethermind.Libp2p (que sí tiene Ed25519 verificado contra el spec).

### CIDs (bafkrei)

- CIDv1 + dag-pb + SHA-256.
- Implementación inline (≈100 LoC) sin dependencias externas (multiformats/BoE).
- Validación: empieza por `bafkrei`, longitud > 8.

### Transporte P2P v0.1: TCP plano

- Funciona en LAN y VPNs sin configuración adicional.
- En v0.3 reemplazado por libp2p (Noise + QUIC + Circuit Relay v2 + DCUtR) para NAT traversal sin abrir puertos.

### Validación de mods

- `ModValidator.Validate()` consume `compatible_versions[]` (con wildcards por segmento) y `dlc_dependencies[]` del manifest.sii.
- Cobertura de versiones probada: 1.55, 1.57, 1.60, 1.61.

### Activación de mods (opt-in)

- `ProfileManager.WriteActiveMods(profileId, mods)`:
  1. Si existe `mod.sii` en `profiles/<id>/` → copia a `mod.sii.bak`.
  2. Reescribe `mod.sii` con la lista en orden.
  3. Si el juego está corriendo, se detectará en el próximo arranque.

## Estado del código (2026-10-03)

| Componente | LoC | Tests |
|---|---|---|
| `ATSync.Core` | ~1.4 K | 26 |
| `ATSync.Cli` | ~400 | — (smoke test manual) |

## Limitaciones conocidas de v0.1

- **NAT**: en v0.1 los peers deben estar en la misma red (LAN/VPN) o con port-forward manual. v0.3 añade Circuit Relay v2.
- **No hay UI gráfica**: la app es sólo CLI. v0.2 añade Avalonia.
- **No hay tray ni auto-arranque**: v0.4.
- **Una identidad por usuario**: no hay multi-cuenta. v0.3+.
- **Tabla de DLCs bundled**: se actualiza manualmente en cada release.