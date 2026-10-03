# ATSync — Guía de Usuario

### Requisitos

- Windows 10/11.
- American Truck Simulator 1.61 (o 1.60) instalado vía Steam.
- .NET 10 Runtime (incluido en el instalador de ATSync).
- ~50 MB en disco para la app + perfiles.

### Instalación desde binarios (cuando esté publicado)

1. Descarga el instalador `ATSync-Setup-x.y.z.exe`.
2. Ejecuta — instala la app y crea un acceso directo en el escritorio.
3. Al abrir por primera vez, ATSync detecta Steam y ATS automáticamente.

### Instalación desde código

```powershell
git clone https://github.com/TheJorseman/ATSync
cd ATSync
dotnet build -c Release
dotnet run --project src/ATSync.Cli -- detect
```

### Flujo típico

#### 1. Crear un perfil

1. Abre ATSync.
2. El panel "Perfiles" muestra tus mods (parseados de `manifest.sii`).
3. Selecciona los mods que quieras incluir, ordénalos y pon un nombre (p.ej. `Convoy_SouthDakota`).
4. ATSync valida cada mod contra tu versión (`compatible_versions[]`) y DLCs instalados.
5. Pulsa "Publicar". ATSync genera un URI `atsync://profile/...` y lo copia al portapapeles.

#### 2. Compartir con amigos

Pega el URI en Discord junto con tu dirección ATSync (la app te la muestra). Ejemplo:

> ```
> atsync://profile/bafkrei…f3a
> atsync://192.168.1.42:6881/
> ```

#### 3. Importar un perfil

1. Abre ATSync → "Importar".
2. Pega el `atsync://…` URI.
3. Pulsa "Conectar". ATSync descarga el perfil y cada mod vía P2P.
4. Cuando termina, te pregunta si quieres **activar los mods en un perfil ATS existente** (opcional, con backup).

#### 4. Activar mods (opt-in)

- Marca "Activar en perfil ATS" en Settings.
- Al importar un perfil, ATSync reescribe `mod.sii` del perfil destino con la lista y orden del perfil ATSync.
- Antes de modificar, ATSync crea `mod.sii.bak` automáticamente.

### Solución de problemas

| Problema | Solución |
|---|---|
| "Steam no encontrado" | Verifica que Steam esté instalado en `C:\Program Files (x86)\Steam`. |
| "ATS no detectado" | Ejecuta el juego al menos una vez para que cree la carpeta `Documents\American Truck Simulator`. |
| "Mod no encontrado en peer" | El peer autor no tiene ese mod en su carpeta mod local; pídele que vuelva a publicar el perfil. |
| "sha256 mismatch" | El archivo fue modificado desde su publicación; pide al autor que lo rehaga. |
| "Versión incompatible" | Tu versión del juego no está en `compatible_versions[]` del mod. Actualiza ATS o el mod. |
| "DLC faltante" | No tienes el DLC que el mod requiere. Cómpralo o elimina el mod del perfil. |

### Privacidad

- ATSync **no sube telemetría** sin tu consentimiento explícito.
- Los URIs compartidos son públicos (es P2P). Para "sólo amigos": v0.4 añadirá perfiles privados con clave compartida.

### Backup y rollback

- Antes de tocar `mod.sii`, ATSync crea `mod.sii.bak`.
- Para revertir: copia `mod.sii.bak` sobre `mod.sii`.
- Los perfiles ATSync están en `%LOCALAPPDATA%\ATSync\profiles\`.