using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eventizo.Data;
using Eventizo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Eventizo.Areas.Identity.Pages.Account.Manage
{
    public class PointHistoryModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PointHistoryModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<UserPointHistory> History { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                History = await _context.UserPointHistories
                    .Where(h => h.UserId == user.Id)
                    .OrderByDescending(h => h.CreatedAt)
                    .ToListAsync();
            }
        }
    }
}
