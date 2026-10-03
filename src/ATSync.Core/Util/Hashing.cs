using System.Security.Cryptography;

namespace ATSync.Core.Util;

/// <summary>
/// Helper de hashing SHA-256 (y SHA-1 cuando se requiera compatibilidad heredada).
/// </summary>
public static class Hashing
{
    public static byte[] Sha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return SHA256.HashData(data);
    }

    public static string Sha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Hex(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256HexOfFile(string path)
    {
        using var fs = File.OpenRead(path);
        return Sha256Hex(fs);
    }
}