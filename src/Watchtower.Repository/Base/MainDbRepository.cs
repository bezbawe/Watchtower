using Watchtower.Entities;

namespace Watchtower.Repository.Base;

public class MainDbRepository<T> : BaseRepository<T, WatchtowerDbContext>
    where T : BaseEntity
{
    public MainDbRepository(WatchtowerDbContext dbContext) : base(dbContext)
    {
    }
}
