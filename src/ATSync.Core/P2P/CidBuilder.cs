using System.Text;
using ATSync.Core.Util;

namespace ATSync.Core.P2P;

/// <summary>
/// Genera CIDs estilo IPFS (CIDv1 con dag-pb + sha2-256) a partir de bytes.
/// Salida: "bafkrei…" (base32 lowercase).
/// </summary>
public static class CidBuilder
{
    private const byte DagPbCodec = 0x71; // dag-pb codec en varint
    private const byte Sha256Hash = 0x12;  // multihash sha2-256

    public static string ComputeCid(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(data, hash);
        return Build(hash.ToArray());
    }

    public static string ComputeCidOfFile(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(fs);
        return Build(hash);
    }

    private static string Build(byte[] sha256)
    {
        // CIDv1: version=1, codec=dag-pb (0x71), multihash=0x12 0x20 <32-byte sha256>
        var raw = new byte[4 + sha256.Length];
        raw[0] = 0x01;             // version=1 varint
        raw[1] = (byte)DagPbCodec; // codec dag-pb varint
        raw[2] = (byte)Sha256Hash; // multihash func
        raw[3] = 0x20;             // length 32
        Buffer.BlockCopy(sha256, 0, raw, 4, sha256.Length);
        return "bafkrei" + Base32Lower(raw);
    }

    /// <summary>Base32 lowercase RFC 4648 (sin padding).</summary>
    private static string Base32Lower(byte[] bytes)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var sb = new StringBuilder(bytes.Length * 2);
        int buffer = bytes[0];
        int bitsLeft = 8;
        int idx = 1;
        while (bitsLeft > 0 || idx < bytes.Length)
        {
            if (bitsLeft < 5)
            {
                if (idx < bytes.Length)
                {
                    buffer <<= 8;
                    buffer |= bytes[idx++];
                    bitsLeft += 8;
                }
                else
                {
                    int pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }
            int idx2 = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            sb.Append(alphabet[idx2]);
        }
        return sb.ToString();
    }

    public static bool IsValid(string cid) =>
        !string.IsNullOrWhiteSpace(cid) && cid.StartsWith("bafkrei") && cid.Length > 8;
}