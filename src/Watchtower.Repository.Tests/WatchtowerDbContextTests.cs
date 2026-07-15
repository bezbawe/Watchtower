using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Watchtower.Repository.Tests;

// Фаза 0: проверяем, что DbContext конструируется и резолвится через DI.
// Тесты конкретных репозиториев появятся в Фазе 1 (когда будут сущности EventRepository/AlertRepository).
public class WatchtowerDbContextTests : BaseTests
{
    [Fact]
    public void DbContext_CanBeConstructed_WithInMemory()
    {
        var options = new DbContextOptionsBuilder<WatchtowerDbContext>()
            .UseInMemoryDatabase("Watchtower_Phase0")
            .Options;

        using var db = new WatchtowerDbContext(options);

        Assert.NotNull(db);
    }

    [Fact]
    public void DbContext_IsResolvedFromDi()
    {
        ServiceCollection.AddDbContext<WatchtowerDbContext>(o => o.UseInMemoryDatabase("Watchtower_Phase0_Di"));

        var db = GetInstance<WatchtowerDbContext>();

        Assert.NotNull(db);
    }
}
