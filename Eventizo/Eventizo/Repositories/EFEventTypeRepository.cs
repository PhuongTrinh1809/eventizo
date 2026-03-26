using Eventizo.Models;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;

namespace Eventizo.Repositories
{
    public class EFEventTypeRepository : IEventTypeRepository
    {
        private readonly ApplicationDbContext _context;

        public EFEventTypeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lấy tất cả danh mục
        public async Task<IEnumerable<EventType>> GetAllAsync()
        {
            return await _context.EventTypes.ToListAsync();
        }

        // Lấy danh mục theo ID
        public async Task<EventType> GetByIdAsync(int id)
        {
            return await _context.EventTypes.Include(et => et.Events).FirstOrDefaultAsync(et => et.Id == id);
        }

        // Thêm danh mục mới
        public async Task AddAsync(EventType eventType)
        {
            await _context.EventTypes.AddAsync(eventType);
            await _context.SaveChangesAsync();
        }

        // Cập nhật danh mục
        public async Task UpdateAsync(EventType eventType)
        {
            _context.EventTypes.Update(eventType);
            await _context.SaveChangesAsync();
        }
    }
}
