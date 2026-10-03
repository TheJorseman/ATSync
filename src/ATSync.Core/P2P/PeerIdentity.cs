using System.Text;

namespace ATSync.Core.P2P;

/// <summary>
/// Identidad P2P persistente: par de claves Ed25519, PeerID libp2p (`12D3KooW…`).
/// Almacenado cifrado con DPAPI en disco (sólo Windows).
/// .NET 10 no expone Ed25519 en `System.Security.Cryptography` de forma portable,
/// así que usamos una clave simétrica de 32 bytes (HMAC-SHA256) como seed para el PeerID.
/// Sigue siendo identificable y persistible — la verificación criptográfica real se hace
/// cuando el motor libp2p Nethermind esté conectado (v2).
/// </summary>
public sealed class PeerIdentity
{
    public string PeerId { get; }
    public byte[] PublicKey { get; }
    public byte[] Seed { get; }                // 32 bytes seed

    public PeerIdentity(byte[] publicKey, byte[] seed)
    {
        PublicKey = publicKey;
        Seed = seed;
        PeerId = ComputePeerId(publicKey);
    }

    /// <summary>PeerID libp2p = SHA-256(pubkey) codificado en base58btc con prefijo '0x12 0x20'.</summary>
    public static string ComputePeerId(byte[] publicKey)
    {
        var multihash = new byte[36];
        multihash[0] = 0x12;       // multicodec identity
        multihash[1] = 0x20;       // SHA-256
        System.Security.Cryptography.SHA256.HashData(publicKey, multihash.AsSpan(2));
        return Base58.Encode(multihash);
    }

    public static PeerIdentity Generate()
    {
        var seed = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var pub = DerivePublic(seed);
        return new PeerIdentity(pub, seed);
    }

    /// <summary>Deriva la "clave pública" del seed. Sustituto Ed255 para v1 (HMAC-SHA256(seed)).</summary>
    public static byte[] DerivePublic(byte[] seed)
    {
        // pub = SHA256(seed || "ed25519-v1")
        var input = new byte[seed.Length + 11];
        Buffer.BlockCopy(seed, 0, input, 0, seed.Length);
        Encoding.ASCII.GetBytes("ed25519-v1").CopyTo(input.AsSpan(seed.Length));
        return System.Security.Cryptography.SHA256.HashData(input);
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        // Sustituto Ed255 para v1: HMAC-SHA256(seed, data)
        using var hmac = new System.Security.Cryptography.HMACSHA256(Seed);
        return hmac.ComputeHash(data.ToArray());
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Seed);
        var expected = hmac.ComputeHash(data.ToArray());
        return signature.SequenceEqual(expected);
    }

    public void Save(string path)
    {
        var blob = Encoding.UTF8.GetBytes(Convert.ToBase64String(PublicKey) + ":" + Convert.ToBase64String(Seed));
        byte[] final = OperatingSystem.IsWindows()
            ? System.Security.Cryptography.ProtectedData.Protect(blob, null, System.Security.Cryptography.DataProtectionScope.CurrentUser)
            : blob;
        File.WriteAllBytes(path, final);
    }

    public static PeerIdentity Load(string path)
    {
        if (!File.Exists(path))
        {
            var newId = Generate();
            newId.Save(path);
            return newId;
        }
        var blob = File.ReadAllBytes(path);
        byte[] plain = OperatingSystem.IsWindows()
            ? System.Security.Cryptography.ProtectedData.Unprotect(blob, null, System.Security.Cryptography.DataProtectionScope.CurrentUser)
            : blob;
        var s = Encoding.UTF8.GetString(plain);
        var ix = s.IndexOf(':');
        if (ix <= 0) throw new InvalidDataException("identity.dat corrupto");
        var pub = Convert.FromBase64String(s[..ix]);
        var seed = Convert.FromBase64String(s[(ix + 1)..]);
        return new PeerIdentity(pub, seed);
    }
}