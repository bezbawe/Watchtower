using Watchtower.Detection.Mitre;

namespace Watchtower.Detection.Tests;

public class MitreAttackCatalogTests
{
    [Fact]
    public void Describe_KnownTechnique_ReturnsIdAndName()
    {
        Assert.Equal("T1110 — Brute Force", MitreAttackCatalog.Describe("T1110"));
    }

    [Fact]
    public void Describe_UnknownTechnique_ReturnsIdUnchanged()
    {
        Assert.Equal("T9999", MitreAttackCatalog.Describe("T9999"));
    }
}
