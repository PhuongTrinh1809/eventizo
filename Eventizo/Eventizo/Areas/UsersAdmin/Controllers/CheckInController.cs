using Eventizo.Data;
using Eventizo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eventizo.Areas.UsersAdmin.Controllers
{
    [Area("UsersAdmin")]
    public class CheckInController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CheckInController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Scan()
        {
            return View();
        }

        [HttpGet("/CheckIn/ViewTicketStatus")]
        public async Task<IActionResult> ViewTicketStatus()
        {
            var ticketsCheckedIn = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Event)
                .Where(t => t.IsCheckedIn)
                .OrderByDescending(t => t.CheckedInAt)
                .ToListAsync();

            return View(ticketsCheckedIn);
        }
    }
}
