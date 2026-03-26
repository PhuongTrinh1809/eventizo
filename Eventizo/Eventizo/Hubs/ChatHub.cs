using Eventizo.Data;
using Eventizo.Helper;
using Eventizo.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eventizo.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(string senderName, string message, string receiverId)
        {
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var senderRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId)) return;

            // 🔍 Tìm hoặc tạo conversation
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c =>
                (c.CustomerId == senderId && c.AdminId == receiverId) ||
                (c.CustomerId == receiverId && c.AdminId == senderId));

            if (conversation == null)
            {
                string adminId, customerId;

                if (senderRole.Contains("Admin"))
                {
                    adminId = senderId;
                    customerId = receiverId;
                }
                else
                {
                    customerId = senderId;
                    adminId = await (from u in _context.Users
                                     join ur in _context.UserRoles on u.Id equals ur.UserId
                                     join r in _context.Roles on ur.RoleId equals r.Id
                                     where r.Name == "Admin"
                                     select u.Id)
                                     .FirstOrDefaultAsync() ?? receiverId;
                }

                conversation = new Conversation
                {
                    AdminId = adminId,
                    CustomerId = customerId,
                    LastUpdated = DateTime.UtcNow
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            // ⚡️ Nhận dạng nếu tin nhắn là ảnh
            bool isImage = message.StartsWith("[img]");

            string encryptedMessage = EncryptionHelper.Encrypt(message);

            var msg = new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderId = senderId,
                ReceiverId = receiverId,
                SenderName = senderName,
                EncryptedMessage = encryptedMessage,
                ImageUrl = isImage ? message.Replace("[img]", "") : null,
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.ChatMessages.Add(msg);
            conversation.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            string timestamp = msg.SentAt.ToString("o");

            // 🧩 Nếu là ảnh → gửi bằng ReceiveImage
            if (isImage)
            {
                string imageUrl = message.Replace("[img]", "");
                await Clients.User(receiverId).SendAsync("ReceiveImage", senderName, imageUrl, senderId, timestamp);
                await Clients.User(senderId).SendAsync("ReceiveImage", senderName, imageUrl, senderId, timestamp);
            }
            else
            {
                // 🔓 Giải mã và gửi text
                string decryptedMessage = EncryptionHelper.Decrypt(encryptedMessage);
                await Clients.User(receiverId).SendAsync("ReceiveMessage", senderName, decryptedMessage, senderId, timestamp);
                await Clients.User(senderId).SendAsync("ReceiveMessage", senderName, decryptedMessage, senderId, timestamp);
            }
        }
    }
}
