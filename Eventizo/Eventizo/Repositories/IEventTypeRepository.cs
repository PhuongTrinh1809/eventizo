using Eventizo.Models;

namespace Eventizo.Repositories
{
    public interface IEventTypeRepository
    {
        Task<IEnumerable<EventType>> GetAllAsync();
        Task<EventType> GetByIdAsync(int id);
        Task AddAsync(EventType eventType);
        Task UpdateAsync(EventType eventType);
    }
}
