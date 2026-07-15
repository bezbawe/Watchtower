using Watchtower.Entities.Enums;
using Watchtower.Entities.Events;
using Watchtower.Repository.Implementations;

namespace Watchtower.Repository.Tests;

[Collection("postgres")]
public class EventRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Add_And_GetById_RoundTrips_WithJsonbFields()
    {
        await using var db = fixture.CreateContext();
        var repo = new EventRepository(db);

        var ev = new LogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "auth-service",
            Severity = Severity.Warning,
            EventType = EventType.LoginFailed,
            Message = "Failed login for user alice",
            Actor = "alice",
            SourceIp = "203.0.113.7",
            GeoCountry = "US",
            Fields = new Dictionary<string, string> { ["attempt"] = "3", ["method"] = "password" }
        };

        var added = await repo.AddAsync(ev);

        // Читаем из свежего контекста, чтобы значение пришло из БД, а не из трекинг-кэша.
        await using var readDb = fixture.CreateContext();
        var loaded = await new EventRepository(readDb).GetByIdAsync(added.Id);

        Assert.NotNull(loaded);
        Assert.Equal("auth-service", loaded!.Source);
        Assert.Equal(EventType.LoginFailed, loaded.EventType);
        Assert.Equal(Severity.Warning, loaded.Severity);
        Assert.Equal("3", loaded.Fields["attempt"]);
        Assert.Equal("password", loaded.Fields["method"]);
    }

    [Fact]
    public async Task SearchAsync_MatchesByActor_CaseInsensitive()
    {
        await using var db = fixture.CreateContext();
        var repo = new EventRepository(db);
        await repo.AddAsync(new LogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "svc",
            Severity = Severity.Info,
            EventType = EventType.LoginSuccess,
            Message = "ok",
            Actor = "BobUnique123"
        });

        var results = await repo.SearchAsync("bobunique", 10);

        Assert.Contains(results, e => e.Actor == "BobUnique123");
    }
}
