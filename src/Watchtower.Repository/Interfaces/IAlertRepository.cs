using Watchtower.Entities.Alerts;
using Watchtower.Repository.Base;

namespace Watchtower.Repository.Interfaces;

public interface IAlertRepository : IBaseRepository<Alert>
{
    Task<List<Alert>> GetActiveAsync(int limit);
}
