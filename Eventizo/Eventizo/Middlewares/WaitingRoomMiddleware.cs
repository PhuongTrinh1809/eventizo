using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Eventizo.Models;
using System.Linq;
using System;
using Eventizo.Data;

namespace Eventizo.Middlewares
{
    public class WaitingRoomMiddleware
    {
        private readonly RequestDelegate _next;
        public static int MaxActiveUsers = 10; // Số người cùng lúc vào hệ thống
        private static readonly object _lock = new object(); // đảm bảo thread-safe

        public WaitingRoomMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string path = context.Request.Path.ToString().ToLower();

            // Cho phép static files đi qua
            if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/images"))
            {
                await _next(context);
                return;
            }

            // Sử dụng session Id làm userId
            string userId = context.Session.Id;
            if (string.IsNullOrEmpty(userId))
            {
                await _next(context);
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            WaitingRoom currentUser;

            lock (_lock) // đảm bảo atomic
            {
                // Lấy user hiện tại trong DB
                currentUser = db.WaitingRooms.FirstOrDefault(x => x.UserId == userId);

                // Nếu chưa có trong DB → thêm mới
                if (currentUser == null)
                {
                    int nextPos = db.WaitingRooms.Any()
                        ? db.WaitingRooms.Max(x => x.Position) + 1
                        : 1;

                    currentUser = new WaitingRoom
                    {
                        UserId = userId,
                        Position = nextPos,
                        JoinTime = DateTime.Now
                    };

                    db.WaitingRooms.Add(currentUser);
                    db.SaveChanges();
                }

                // Lấy danh sách những người đang active (Position = 0)
                var activeUsers = db.WaitingRooms.Where(x => x.Position == 0).OrderBy(x => x.JoinTime).ToList();

                // Nếu còn slot → cho người này vào hệ thống
                if (currentUser.Position > 0 && activeUsers.Count < MaxActiveUsers)
                {
                    currentUser.Position = 0;
                    context.Session.SetInt32("QueuePosition", 0);
                    db.SaveChanges();
                }
                else
                {
                    // Đặt QueuePosition cho người đang chờ
                    context.Session.SetInt32("QueuePosition", currentUser.Position);
                }
            }

            // Nếu người đang chờ → redirect về WaitingRoom
            if (currentUser.Position > 0 && !path.StartsWith("/waitingroom"))
            {
                context.Response.Redirect("/WaitingRoom/Index");
                return;
            }

            await _next(context);
        }
    }
}
