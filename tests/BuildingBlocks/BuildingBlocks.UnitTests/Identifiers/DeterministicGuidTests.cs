using BuildingBlocks.Identifiers;

namespace BuildingBlocks.UnitTests.Identifiers;

public sealed class DeterministicGuidTests
{
    // The namespace the catalog seed uses; the Go port derives its ids from the
    // same value, so these expectations also pin cross-language agreement.
    private static readonly Guid SeedNamespace = new("6f2c1e64-9d0f-4a1e-9c1a-2d1f2b8f4c31");

    [Theory]
    [InlineData("iPhone X", "aed705de-0368-5fba-be0a-0594eede2efa")]
    [InlineData("Samsung 10", "07b14e76-aad3-51f9-8b7c-a2428b9e757e")]
    [InlineData("Huawei Plus", "f3afd807-4f59-5baf-a891-9f9e6f818725")]
    [InlineData("Xiaomi Mi 9", "f022c00d-b3d1-5829-b889-2ba81e9d6fff")]
    public void Create_ProducesTheSameIdsAsTheGoPort(string name, string expected)
    {
        Assert.Equal(new Guid(expected), DeterministicGuid.Create(SeedNamespace, name));
    }

    [Fact]
    public void Create_IsStableAcrossCalls()
    {
        var first = DeterministicGuid.Create(SeedNamespace, "iPhone X");
        var second = DeterministicGuid.Create(SeedNamespace, "iPhone X");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_DifferentNames_ProduceDifferentIds()
    {
        Assert.NotEqual(
            DeterministicGuid.Create(SeedNamespace, "iPhone X"),
            DeterministicGuid.Create(SeedNamespace, "Samsung 10"));
    }

    [Fact]
    public void Create_DifferentNamespaces_ProduceDifferentIds()
    {
        Assert.NotEqual(
            DeterministicGuid.Create(SeedNamespace, "iPhone X"),
            DeterministicGuid.Create(Guid.Empty, "iPhone X"));
    }

    [Fact]
    public void Create_SetsVersionFiveAndRfc4122Variant()
    {
        var bytes = DeterministicGuid.Create(SeedNamespace, "iPhone X").ToByteArray();

        // Guid stores the first three fields little-endian, so the version nibble
        // lives in byte 7 and the variant bits in byte 8.
        Assert.Equal(0x50, bytes[7] & 0xF0);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }
}
