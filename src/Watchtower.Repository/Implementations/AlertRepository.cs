using Microsoft.EntityFrameworkCore;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;
using Watchtower.Repository.Base;
using Watchtower.Repository.Interfaces;

namespace Watchtower.Repository.Implementations;

public class AlertRepository(WatchtowerDbContext dbContext)
    : MainDbRepository<Alert>(dbContext), IAlertRepository
{
    public async Task<List<Alert>> GetActiveAsync(int limit)
    {
        return await db.Alerts
            .AsNoTracking()
            .Where(a => a.Status != AlertStatus.Resolved)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Alert>> GetByPeriodAsync(DateTimeOffset from, DateTimeOffset to, int maxRows)
    {
        return await db.Alerts
            .AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxRows)
            .ToListAsync();
    }
}
