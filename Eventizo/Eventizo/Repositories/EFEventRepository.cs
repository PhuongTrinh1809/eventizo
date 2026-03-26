using Eventizo.Models;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;

namespace Eventizo.Repositories
{
    public class EFEventRepository : IEventRepository
    {
        private readonly ApplicationDbContext _context;

        public EFEventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Event>> GetAllAsync()
        {
            // return await _context.Events.ToListAsync(); 
            return await _context.Events.Include(p => p.EventType).ToListAsync();

        }

        public async Task<Event> GetByIdAsync(int id)
        {
            // return await _context.Events.FindAsync(id); 
            // lấy thông tin kèm theo EventType 
            return await _context.Events.Include(p => p.EventType).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddAsync(Event even)
        {
            _context.Events.Add(even);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Event even)
        {
            _context.Events.Update(even);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var even = await _context.Events.FindAsync(id);
            _context.Events.Remove(even);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateStatusAsync(int id, string status)
        {
            var even = await _context.Events.FindAsync(id);
            if (even != null)
            {
                even.Status = status;
                _context.Entry(even).Property(e => e.Status).IsModified = true;
                await _context.SaveChangesAsync();
            }
        }

    }
}
