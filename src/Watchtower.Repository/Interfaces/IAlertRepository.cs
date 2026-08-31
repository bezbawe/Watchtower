using Watchtower.Entities.Alerts;
using Watchtower.Repository.Base;

namespace Watchtower.Repository.Interfaces;

public interface IAlertRepository : IBaseRepository<Alert>
{
    Task<List<Alert>> GetActiveAsync(int limit);

    // Алерты за период [from; to] по CreatedAt (для PDF-отчёта по инцидентам).
    Task<List<Alert>> GetByPeriodAsync(DateTimeOffset from, DateTimeOffset to, int maxRows);
}
