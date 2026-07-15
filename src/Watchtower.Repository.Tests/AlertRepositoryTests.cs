using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Repository.Implementations;

namespace Watchtower.Repository.Tests;

[Collection("postgres")]
public class AlertRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Add_And_GetById_RoundTrips_WithArrays()
    {
        await using var db = fixture.CreateContext();
        var repo = new AlertRepository(db);

        var alert = new Alert
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Severity = AlertSeverity.High,
            DetectorName = "BruteForceRule",
            Title = "Brute force from 203.0.113.7",
            Explanation = "12 failed logins in 3 minutes from a single IP",
            MitreTechniques = new List<string> { "T1110" },
            RelatedEventIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            Status = AlertStatus.New
        };

        var added = await repo.AddAsync(alert);

        await using var readDb = fixture.CreateContext();
        var loaded = await new AlertRepository(readDb).GetByIdAsync(added.Id);

        Assert.NotNull(loaded);
        Assert.Equal(AlertSeverity.High, loaded!.Severity);
        Assert.Equal(AlertStatus.New, loaded.Status);
        Assert.Equal(new[] { "T1110" }, loaded.MitreTechniques);
        Assert.Equal(2, loaded.RelatedEventIds.Count);
    }

    [Fact]
    public async Task GetActiveAsync_ExcludesResolved()
    {
        await using var db = fixture.CreateContext();
        var repo = new AlertRepository(db);

        var resolvedTitle = "resolved-" + Guid.NewGuid();
        var activeTitle = "active-" + Guid.NewGuid();
        await repo.AddAsync(new Alert { Severity = AlertSeverity.Low, DetectorName = "X", Title = resolvedTitle, Explanation = "e", Status = AlertStatus.Resolved });
        await repo.AddAsync(new Alert { Severity = AlertSeverity.Low, DetectorName = "X", Title = activeTitle, Explanation = "e", Status = AlertStatus.New });

        var active = await repo.GetActiveAsync(100);

        Assert.Contains(active, a => a.Title == activeTitle);
        Assert.DoesNotContain(active, a => a.Title == resolvedTitle);
    }
}
