using Eventizo.Helper;
using Eventizo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static Microsoft.AspNetCore.Razor.Language.TagHelperMetadata;
using Eventizo.Data;

namespace Eventizo.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 🔹 Trang chính Chat
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            // 🔹 Lấy admin đầu tiên trong hệ thống
            var admin = (await _userManager.GetUsersInRoleAsync("Admin")).FirstOrDefault();
            if (admin == null)
            {
                ViewBag.AdminId = "";
                ViewBag.Error = "Không có quản trị viên nào khả dụng.";
                return View();
            }

            ViewBag.AdminId = admin.Id;
            ViewBag.UserId = user.Id;
            ViewBag.UserName = user.UserName;
            return View();
        }

        // 🔹 Tạo hoặc lấy cuộc trò chuyện giữa user và admin
        [HttpGet]
        public async Task<IActionResult> GetOrCreateConversation(string adminId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(adminId))
                return BadRequest("Thiếu thông tin người dùng hoặc admin.");

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    (c.CustomerId == currentUserId && c.AdminId == adminId) ||
                    (c.CustomerId == adminId && c.AdminId == currentUserId));

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    CustomerId = currentUserId,
                    AdminId = adminId,
                    LastUpdated = DateTime.UtcNow
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            return Json(new { id = conversation.Id, adminId });
        }

        // 🔹 Lấy danh sách tin nhắn
        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            var rawMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var messages = rawMessages.Select(m =>
            {
                var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                var vnTime = TimeZoneInfo.ConvertTimeFromUtc(m.SentAt, vnTimeZone); // Chỉ để log/local nếu muốn
                return new
                {
                    senderId = m.SenderId,
                    receiverId = m.ReceiverId,
                    message = string.IsNullOrEmpty(m.EncryptedMessage) ? null : EncryptionHelper.Decrypt(m.EncryptedMessage),
                    imageUrl = m.ImageUrl,
                    timestamp = m.SentAt.ToUniversalTime().ToString("o") // gửi chuẩn ISO UTC
                };
            }).ToList();

            return Json(messages);
        }

        // 🔹 Upload ảnh
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return Json(new { success = false, message = "Không có tệp nào được tải lên." });

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/{fileName}";
            return Json(new { success = true, filePath = relativePath });
        }
    }
}
