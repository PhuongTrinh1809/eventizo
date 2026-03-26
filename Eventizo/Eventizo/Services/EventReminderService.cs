using Eventizo.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Eventizo.Services
{
    public class EventReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public EventReminderService(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task SendWeeklyRemindersAsync()
        {
            // 1. Xác định khoảng thời gian 7 ngày tới
            var today = DateTime.Now.Date;
            var sevenDaysLater = today.AddDays(7);

            // 2. Lấy danh sách sự kiện sắp diễn ra (Dùng EventStartingDate)
            var upcomingEvents = await _context.Events
                .Where(e => e.EventStartingDate >= today && e.EventStartingDate <= sevenDaysLater)
                .ToListAsync();

            if (!upcomingEvents.Any()) return;

            // 3. Lấy danh sách tất cả khách hàng
            var users = await _context.Users.Where(u => !string.IsNullOrEmpty(u.Email)).ToListAsync();

            // 4. Chuẩn bị nội dung email
            string eventListHtml = "<ul>";
            foreach (var ev in upcomingEvents)
            {
                // Hiển thị ngày bắt đầu EventStartingDate lên email
                eventListHtml += $"<li><strong>{ev.Name}</strong> - Bắt đầu: {ev.EventStartingDate:dd/MM/yyyy}</li>";
            }
            eventListHtml += "</ul>";

            // 5. Gửi email cho từng người
            foreach (var user in users)
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "🔔 Sự kiện hấp dẫn trong 7 ngày tới!",
                        $"Chào {user.FullName},<br/>Đừng bỏ lỡ các sự kiện sắp tới tại Eventizo:<br/>{eventListHtml}<br/>Truy cập website để đặt vé ngay!"
                    );
                }
                catch { /* Log lỗi nếu cần */ }
            }
        }
    }
}