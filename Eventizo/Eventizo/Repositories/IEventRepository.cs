using Eventizo.Models;

namespace Eventizo.Repositories
{
    public interface IEventRepository
    {
        Task<IEnumerable<Event>> GetAllAsync();
        Task<Event> GetByIdAsync(int id);
        Task AddAsync(Event even);
        Task UpdateAsync(Event even);
        Task DeleteAsync(int id);
        Task UpdateStatusAsync(int id, string status);

    }
}
