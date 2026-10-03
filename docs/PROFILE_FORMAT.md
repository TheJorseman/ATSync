# ATSync — Formato del perfil ATSync (v1)

## URI

```
atsync://profile/<profile_cid>
atsync://mod/<mod_cid>
```

- `<profile_cid>` = CIDv1 dag-pb + SHA-256 del JSON serializado del perfil.
- `<mod_cid>`     = CIDv1 dag-pb + SHA-256 del `.scs` (binario completo).

## JSON del perfil

```json
{
  "schema":  "atsync.profile/v1",
  "id":      "12D3KooW…",            // PeerID libp2p del autor (Ed25519)
  "name":    "VTC_Convoy_SouthDakota_v3",
  "createdAt":"2026-10-03T09:30:00Z",
  "target": {
    "game":          "ats",          // siempre "ats" en v1
    "version":       "1.61.*",       // wildcard
    "dlcsRequired": ["dlc_arizona", "dlc_south_dakota"]
  },
  "mods": [
    {
      "filename":           "promods_ats_261.scs",
      "size":               412345678,
      "sha256":             "ab12…",   // hex SHA-256 del .scs
      "cid":                "bafkrei…",// content-address IPFS-style
      "loadOrder":          10,        // orden de prioridad
      "displayName":        "ProMods ATS 2.6.1",
      "packageVersion":     "2.6.1",
      "compatibleVersions": ["1.60.*", "1.61.*"],
      "dlcDependencies":    ["dlc_arizona"],
      "source":             "local"
    }
  ]
}
```

## Wire format (ProfileTransfer v1)

Línea de texto UTF-8, terminada con `\n`. Para datos binarios: línea con tamaño + nombre + bytes raw.

```
HELLO <peerId>\n                  → cliente se identifica (libp2p PeerID del emisor)
                                    servidor responde "220 OK\n"
GETPROFILE <profile_cid>\n         → servidor responde "200 <len>\n" + JSON
                                    ó "404\n" si no encontrado
GETMOD <mod_cid>\n                 → servidor responde "200 <size> <filename>\n" + raw bytes
```

## Reglas de validación (en el cliente)

1. **Versión**: el perfil se acepta si la versión instalada del juego coincide con `target.version` (wildcard `*` por segmento).
2. **DLCs**: si `target.dlcsRequired` lista un DLC que el usuario no tiene, se muestra una advertencia y los mods dependientes fallan al activarse.
3. **SHA-256**: cada mod descargado se hashea; si no coincide con `mods[i].sha256`, se descarta.
4. **CID**: el archivo debe coincidir con `mods[i].cid`; cualquier inconsistencia se reporta.

## Cambio de versión

- **v0.1** (este formato): TCP, CID dag-pb SHA-256.
- **v0.3** (planeado): transporte libp2p (Noise/QUIC/WS), protocolo `/atsync/profile/1.0.0` (libp2p protocol ID).
- **v0.4** (planeado): bloques cifrados con AES-256 + PSK para perfiles privados.