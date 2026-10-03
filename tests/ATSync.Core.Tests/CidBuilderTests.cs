using ATSync.Core.P2P;

namespace ATSync.Core.Tests;

public class CidBuilderTests
{
    [Fact]
    public void ComputesDeterministicCid()
    {
        var data = "hello world"u8.ToArray();
        var cid1 = CidBuilder.ComputeCid(data);
        var cid2 = CidBuilder.ComputeCid(data);
        Assert.Equal(cid1, cid2);
        Assert.StartsWith("bafkrei", cid1);
    }

    [Fact]
    public void DifferentDataProducesDifferentCid()
    {
        var c1 = CidBuilder.ComputeCid("foo"u8.ToArray());
        var c2 = CidBuilder.ComputeCid("bar"u8.ToArray());
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void IsValidCid()
    {
        Assert.True(CidBuilder.IsValid(CidBuilder.ComputeCid("x"u8.ToArray())));
        Assert.False(CidBuilder.IsValid(""));
        Assert.False(CidBuilder.IsValid("invalid"));
    }

    [Fact]
    public void ComputeCidOfFileMatchesComputeCid()
    {
        var path = Path.Combine(Path.GetTempPath(), "atsync_cid_test_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, "test"u8.ToArray());
        try
        {
            var a = CidBuilder.ComputeCid("test"u8.ToArray());
            var b = CidBuilder.ComputeCidOfFile(path);
            Assert.Equal(a, b);
        }
        finally
        {
            File.Delete(path);
        }
    }
}