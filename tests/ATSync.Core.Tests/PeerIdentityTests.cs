using ATSync.Core.P2P;

namespace ATSync.Core.Tests;

public class PeerIdentityTests
{
    [Fact]
    public void GeneratesValidPeerId()
    {
        var id = PeerIdentity.Generate();
        Assert.NotEmpty(id.PeerId);
        // El prefijo exacto depende del hash sha256 → sólo validamos longitud razonable y caracteres base58.
        Assert.All(id.PeerId, c => Assert.Contains(c.ToString(), "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"));
        Assert.InRange(id.PeerId.Length, 40, 60); // multihash 36 bytes → ~ 49 chars
    }

    [Fact]
    public void SaveAndLoad()
    {
        var id = PeerIdentity.Generate();
        var tmp = Path.Combine(Path.GetTempPath(), $"atsync_id_{Guid.NewGuid():N}.bin");
        try
        {
            id.Save(tmp);
            var loaded = PeerIdentity.Load(tmp);
            Assert.Equal(id.PeerId, loaded.PeerId);
            Assert.Equal(id.PublicKey, loaded.PublicKey);
            Assert.Equal(id.Seed, loaded.Seed);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void AutoGeneratesWhenFileMissing()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"atsync_id_{Guid.NewGuid():N}.bin");
        try
        {
            Assert.False(File.Exists(tmp));
            var id = PeerIdentity.Load(tmp);
            Assert.NotEmpty(id.PeerId);
            Assert.True(File.Exists(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private static int ComputePeerIdMultihashLength(PeerIdentity id)
    {
        var b58 = id.PeerId;
        // Cada PeerID libp2p empieza por 12D3KooW que es la base58btc de los 2 primeros bytes 0x12 0x20
        // El prefijo es de longitud 8 caracteres para 0x12 0x20 + 32 bytes restantes.
        // El total varía, pero siempre será ≥ 50 chars.
        return b58.Length;
    }
}