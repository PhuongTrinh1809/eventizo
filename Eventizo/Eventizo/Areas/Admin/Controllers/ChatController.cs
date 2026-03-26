using Eventizo.Data;
using Eventizo.Hubs;
using Eventizo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Eventizo.Helper;
using Eventizo.Services;
using Eventizo.Helper;


namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly EmailService _emailService;


        public ChatController(ApplicationDbContext context, IHubContext<ChatHub> hubContext, EmailService emailService)
        {
            _context = context;
            _hubContext = hubContext;
            _emailService = emailService;
        }


        public IActionResult Index() => View();

        // ✅ Danh sách khách hàng có hội thoại
        [HttpGet]
        public async Task<IActionResult> GetCustomerList()
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var conversations = await _context.Conversations
                .Include(c => c.Customer)
                .Include(c => c.Admin)
                .Where(c => c.AdminId == adminId)
                .OrderByDescending(c => c.LastUpdated)
                .Select(c => new
                {
                    c.Id,
                    c.CustomerId,
                    CustomerName = c.Customer.UserName,
                    AdminId = c.AdminId,
                    LastUpdated = c.LastUpdated // ❌ bỏ AddHours(7)
                })
                .ToListAsync();

            return Json(conversations);
        }

        // ✅ Lấy tin nhắn (text / image)
        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.SenderName,
                    Message = !string.IsNullOrEmpty(m.ImageUrl)
                        ? null
                        : EncryptionHelper.Decrypt(m.EncryptedMessage),
                    m.ImageUrl,
                    CreatedAt = m.CreatedAt // ❌ bỏ AddHours(7)
                })
                .ToListAsync();

            return Json(messages);
        }

        // ✅ Gửi tin nhắn từ admin
        [HttpPost]
        public async Task<IActionResult> SendMessage(string customerId, string content)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var adminName = User.Identity?.Name ?? "Admin";

            if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(content))
                return BadRequest();
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.AdminId == adminId && c.CustomerId == customerId);

            var now = DateTime.UtcNow; // ✅ lưu UTC

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    AdminId = adminId,
                    CustomerId = customerId,
                    LastUpdated = now
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            bool isImage = content.StartsWith("[img]");

            var message = new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderId = adminId,
                ReceiverId = customerId,
                SenderName = adminName,
                EncryptedMessage = isImage ? string.Empty : EncryptionHelper.Encrypt(content),
                ImageUrl = isImage ? content.Replace("[img]", "") : null,
                SentAt = now,
                CreatedAt = now,
                IsRead = false
            };

            _context.ChatMessages.Add(message);
            conversation.LastUpdated = now;
            await _context.SaveChangesAsync();
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Id == customerId);
            if (customer != null && !string.IsNullOrEmpty(customer.Email))
            {
                string subject = $"Tin nhắn mới từ {adminName}";
                string body = $@"
        <p>Xin chào {customer.UserName},</p>
        <p>Bạn vừa nhận được một tin nhắn mới từ quản trị viên.</p>
        <p><b>Nội dung:</b> {content}</p>
        <p>Vui lòng đăng nhập vào hệ thống để xem chi tiết.</p>
        <br/>
        <p>Trân trọng,<br/>Đội ngũ hỗ trợ</p>";

                // Gọi nền để gửi mail sau 10s (không chặn request)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10)); // đổi thành FromMinutes(1) nếu cần 1 phút
                        await _emailService.SendEmailAsync(customer.Email, subject, body); // dùng EmailService đã inject
                        Console.WriteLine($"[✅] Đã gửi email cho {customer.Email} lúc {DateTime.Now}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[❌] Lỗi gửi email: {ex.Message}");
                    }
                });
            }



            // ✅ Gửi theo ISO string (client tự xử lý timezone)
            string timestamp = message.SentAt.ToString("o");

            if (isImage)
            {
                await _hubContext.Clients.User(customerId)
                    .SendAsync("ReceiveImage", adminName, message.ImageUrl, adminId, timestamp);
            }
            else
            {
                string decrypted = EncryptionHelper.Decrypt(message.EncryptedMessage);
                await _hubContext.Clients.User(customerId)
                    .SendAsync("ReceiveMessage", adminName, decrypted, adminId, timestamp);
            }

            return Json(new { success = true });
        }

        // ✅ Upload ảnh chat
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile image, string receiverId)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No image uploaded");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var imageUrl = $"/uploads/chat/{fileName}";
            return Json(new { url = imageUrl });
        }

        // ✅ Load tất cả cuộc trò chuyện của admin
        [HttpGet]
        public async Task<IActionResult> GetAdminConversations()
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var conversations = await _context.Conversations
                .Include(c => c.Customer)
                .Where(c => c.AdminId == adminId && c.CustomerId != adminId)
                .GroupBy(c => new { c.CustomerId, c.Customer.FullName, c.Customer.UserName })
                .Select(g => new
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = !string.IsNullOrEmpty(g.Key.FullName)
                        ? g.Key.FullName
                        : g.Key.UserName,
                    LastUpdated = g.Max(x => x.LastUpdated) // ❌ bỏ AddHours(7)
                })
                .OrderByDescending(x => x.LastUpdated)
                .ToListAsync();

            return Json(conversations);
        }

        // ✅ Đánh dấu đã đọc
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(string customerId)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var messages = await _context.ChatMessages
                .Where(m => m.ReceiverId == adminId && m.SenderId == customerId && !m.IsRead)
                .ToListAsync();

            if (messages.Any())
            {
                foreach (var msg in messages)
                    msg.IsRead = true;

                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // ✅ Đếm tin chưa đọc
        [HttpGet]
        public async Task<IActionResult> GetUnreadCounts()
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var unreadCounts = await _context.ChatMessages
                .Where(m => m.ReceiverId == adminId && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(unreadCounts);
        }

        // ✅ Lấy tin nhắn theo khách hàng
        [HttpGet]
        public async Task<IActionResult> GetMessagesByCustomer(string customerId)
        {
            var messages = await _context.ChatMessages
                .Include(m => m.Conversation)
                .Where(m => m.Conversation.CustomerId == customerId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.SenderName,
                    MessageText = !string.IsNullOrEmpty(m.ImageUrl)
                        ? $"[img]{m.ImageUrl}"
                        : EncryptionHelper.Decrypt(m.EncryptedMessage),
                    CreatedAt = m.CreatedAt // ❌ bỏ AddHours(7)
                })
                .ToListAsync();

            return Json(messages);
        }

    }


}