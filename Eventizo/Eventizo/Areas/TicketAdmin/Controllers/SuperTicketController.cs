using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;

namespace Eventizo.Areas.TicketAdmin.Controllers
{
    [Area("TicketAdmin")]
    public class SuperTicketController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuperTicketController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var eventsTickets = await _context.Events
                .Include(e => e.Tickets)
                .Include(e => e.EventType)
                .OrderBy(e => e.EventStartingDate)
                .ToListAsync();

            return View(eventsTickets);
        }

        public IActionResult Canceled()
        {
            return View(Canceled);
        }
        public IActionResult Leftover()
        {
            return View(Leftover);
        }
        public async Task<IActionResult> Sold()
        {
            var tickets = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.User)
                .OrderByDescending(t => t.BookedAt)
                .ToListAsync();

            return View(tickets);
        }
    }
}
