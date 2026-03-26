using Microsoft.AspNetCore.Mvc;

namespace Eventizo.Areas.UsersAdmin.Controllers
{
    [Area("UsersAdmin")]
    public class SuperUsersController : Controller
    {
        public IActionResult Index()
        {
            return View(Index);
        }
        public IActionResult Concert()
        {
            return View(Concert);
        }
        public IActionResult Liveshow()
        {
            return View(Liveshow);
        }
        public IActionResult Tour()
        {
            return View(Tour);
        }
    }
}
