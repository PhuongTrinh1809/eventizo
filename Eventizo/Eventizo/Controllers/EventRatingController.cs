using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;
using Eventizo.Models;
using Eventizo.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using Eventizo.Services;

namespace Eventizo.Controllers
{
    public class EventRatingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PointService _pointService;

        public EventRatingController(ApplicationDbContext context)
        {
            _context = context;
            _pointService = new PointService(context);
        }

        // ✅ Hiển thị danh sách sự kiện
        public IActionResult Index()
        {
            string userIdentifier;
            string? userId = null;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                userIdentifier = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            else
            {
                userIdentifier = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }

            var events = _context.Events
                .Include(e => e.Ratings)
                .Include(e => e.FavoriteVotes)
                .ToList()
                .Select(e =>
                {
                    // 🕒 Gán Start và End đúng từ EventDate
                    DateTime start = e.EventStartingDate; // Nếu muốn có giờ, combine với StartingTime
                    DateTime end = e.EventEndingDate;   // Nếu muốn có giờ, combine với EndingTime

                    return new EventWithRatingsViewModel
                    {
                        EventId = e.Id,
                        EventName = e.Name,
                        ImageUrl = e.ImageUrl,
                        EventDate = e.EventStartingDate,
                        Place = e.Place,
                        Ratings = e.Ratings.OrderByDescending(r => r.CreatedAt).ToList(),
                        FavoriteVotes = e.FavoriteVotes.Count,
                        IsUserVoted = e.FavoriteVotes.Any(v =>
                            v.UserIdentifier == userIdentifier &&
                            v.CreatedAt.Date == DateTime.Now.Date),
                        HasRated = userId != null && e.Ratings.Any(r => r.UserId == userId),
                        Start = start, // ✅ gán đúng Start
                        End = end      // ✅ gán đúng End
                    };
                })
                .OrderByDescending(e => e.AverageRating)
                .ToList();

            // ✅ Hiển thị thông tin điểm & tiến trình
            if (userId != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    ViewBag.UserPoints = user.Points;
                    ViewBag.MemberLevel = user.MemberLevel;
                    ViewBag.NextTarget = _pointService.GetNextLevelTarget(user.Points);
                    ViewBag.ProgressPercent = _pointService.GetProgressPercentage(user.Points);
                }
            }

            return View(events);
        }


        // ✅ Gửi đánh giá (1 lần/sự kiện)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int EventId, int Rating, string? Comment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name; // ✅ LẤY USERNAME

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "❌ Không xác định được người dùng.";
                return RedirectToAction("Index");
            }

            // 🔒 Kiểm tra đã đánh giá chưa
            bool alreadyRated = _context.EventRatings
                .Any(r => r.EventId == EventId && r.UserId == userId);

            if (alreadyRated)
            {
                TempData["ErrorMessage"] = "⚠️ Bạn chỉ có thể đánh giá 1 lần cho mỗi sự kiện.";
                return RedirectToAction("Index");
            }

            if (Rating < 1 || Rating > 5)
            {
                TempData["ErrorMessage"] = "❌ Số sao không hợp lệ.";
                return RedirectToAction("Index");
            }

            var newRating = new EventRating
            {
                EventId = EventId,
                UserId = userId,
                UserName = userName,        
                Rating = Rating,
                Comment = Comment?.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.EventRatings.Add(newRating);
            _context.SaveChanges();

            // 🎯 Cộng điểm
            int points = string.IsNullOrWhiteSpace(Comment) ? 5 : 7;
            _pointService.AddPoints(userId, points);

            TempData["SuccessMessage"] = "🎉 Cảm ơn bạn đã đánh giá!";
            return RedirectToAction("Index");
        }

        // ❤️ Bình chọn yêu thích (giới hạn 3 lần/ngày)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VoteFavorite(int EventId)
        {
            string userIdentifier;
            bool isAuthenticated = User.Identity != null && User.Identity.IsAuthenticated;

            if (isAuthenticated)
                userIdentifier = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            else
                userIdentifier = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var today = DateTime.Now.Date;

            // 🔒 Kiểm tra đã bình chọn sự kiện này hôm nay chưa
            bool alreadyVotedToday = _context.FavoriteVotes.Any(v =>
                v.EventId == EventId &&
                v.UserIdentifier == userIdentifier &&
                v.CreatedAt.Date == today);

            if (alreadyVotedToday)
            {
                TempData["ErrorMessage"] = "⚠️ Bạn đã bình chọn sự kiện này hôm nay rồi.";
                return RedirectToAction("Index");
            }

            // 🔒 Giới hạn tổng 3 bình chọn/ngày
            int dailyVoteCount = _context.FavoriteVotes
                .Count(v => v.UserIdentifier == userIdentifier && v.CreatedAt.Date == today);

            if (dailyVoteCount >= 3)
            {
                TempData["ErrorMessage"] = "🚫 Bạn chỉ được bình chọn tối đa 3 sự kiện mỗi ngày.";
                return RedirectToAction("Index");
            }

            // ✅ Ghi nhận vote
            var vote = new FavoriteVote
            {
                EventId = EventId,
                UserIdentifier = userIdentifier,
                CreatedAt = DateTime.Now
            };

            _context.FavoriteVotes.Add(vote);
            _context.SaveChanges();

            // 🎯 Cộng điểm
            if (isAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _pointService.AddPoints(userId, 5);
            }

            TempData["SuccessMessage"] = "❤️ Cảm ơn bạn đã bình chọn!";
            return RedirectToAction("Index");
        }

        // ❌ Xóa đánh giá (trừ điểm)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteRating(int ratingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var rating = _context.EventRatings.FirstOrDefault(r => r.Id == ratingId && r.UserId == userId);

            if (rating == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đánh giá của bạn.";
                return RedirectToAction("Index");
            }

            _context.EventRatings.Remove(rating);
            _context.SaveChanges();

            _pointService.DeductPoints(userId, 5);

            TempData["SuccessMessage"] = "🗑️ Đánh giá của bạn đã bị xóa (trừ 5 điểm).";
            return RedirectToAction("Index");
        }

        // 🎫 Mua vé (cộng điểm theo loại vé)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BuyTicket(int EventId, bool isVip)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Ở đây bạn có thể thêm logic lưu vé vào DB nếu cần
            // VD: _context.Tickets.Add(new Ticket { EventId = EventId, UserId = userId, IsVip = isVip });

            // ✅ Cộng điểm mua vé
            _pointService.AddPointsForTicket(userId, isVip);

            TempData["SuccessMessage"] = isVip
                ? "🎉 Bạn đã mua vé VIP! Điểm thưởng đã được cộng."
                : "🎉 Bạn đã mua vé thường! Điểm thưởng đã được cộng.";

            return RedirectToAction("Index");
        }
    }
}
