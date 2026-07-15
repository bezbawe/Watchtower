using Watchtower.Entities.Events;
using Watchtower.Repository.Base;

namespace Watchtower.Repository.Interfaces;

public interface IEventRepository : IBaseRepository<LogEvent>
{
    Task<List<LogEvent>> GetRecentAsync(int limit);
    Task<List<LogEvent>> SearchAsync(string term, int limit);
}
