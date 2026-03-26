using Eventizo.Models;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;

namespace Eventizo.Repositories
{
    public class EFCustomerRepository : ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public EFCustomerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _context.Customers
                         .Include(c => c.User)
                         .ToListAsync();
        }

        public async Task<Customer> GetByIdAsync(string id)
        {
            return await _context.Customers
                         .Include(c => c.User)
                         .FirstOrDefaultAsync(c => c.UserId == id); 
        }

    }
}
