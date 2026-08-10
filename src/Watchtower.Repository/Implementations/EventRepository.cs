using Microsoft.EntityFrameworkCore;
using Watchtower.Entities.Events;
using Watchtower.Repository.Base;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Repository.Implementations;

public class EventRepository(WatchtowerDbContext dbContext)
    : MainDbRepository<LogEvent>(dbContext), IEventRepository
{
    public async Task<List<LogEvent>> GetRecentAsync(int limit)
    {
        return await db.LogEvents
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<LogEvent>> GetSinceAsync(DateTimeOffset since, int maxRows)
    {
        return await db.LogEvents
            .AsNoTracking()
            .Where(e => e.Timestamp >= since)
            .OrderByDescending(e => e.Timestamp)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<List<LogEvent>> SearchAsync(string term, int limit)
    {
        var pattern = $"%{term.Trim()}%";
        return await db.LogEvents
            .AsNoTracking()
            .Where(e => EF.Functions.ILike(e.Message, pattern)
                        || (e.Actor != null && EF.Functions.ILike(e.Actor, pattern))
                        || EF.Functions.ILike(e.Source, pattern))
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync();
    }
}
