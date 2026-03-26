using Microsoft.AspNetCore.Mvc;
using Eventizo.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Eventizo.Data;
namespace Eventizo.Controllers
{
    public class WaitingRoomController : Controller
    {
        private readonly ApplicationDbContext _db;

        public WaitingRoomController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            string userId = HttpContext.Session.Id;
            var user = _db.WaitingRooms.FirstOrDefault(x => x.UserId == userId);

            // ✅ Xóa user hết hạn (quá 1 phút không request)
            var expiredUsers = _db.WaitingRooms
                .Where(x => x.Position == 0 && x.LastActive < DateTime.Now.AddMinutes(-1))
                .ToList();

            if (expiredUsers.Any())
            {
                _db.WaitingRooms.RemoveRange(expiredUsers);
                _db.SaveChanges();
            }

            if (user == null)
            {
                ViewBag.Position = -1;
                return View();
            }

            // ✅ update LastActive mỗi lần load
            user.LastActive = DateTime.Now;
            _db.SaveChanges();

            // Nếu user đã được vào → không render view nữa, ném sang Home
            if (user.Position == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Tính vị trí thực tế trong hàng
            int realPosition = _db.WaitingRooms
                .Where(x => x.Position > 0 && x.JoinTime < user.JoinTime)
                .Count() + 1;

            ViewBag.Position = realPosition;

            return View();
        }

    }
}
