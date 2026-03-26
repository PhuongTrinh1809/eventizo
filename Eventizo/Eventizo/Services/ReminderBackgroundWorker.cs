using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eventizo.Services
{
    public class ReminderBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _services;

        public ReminderBackgroundWorker(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Worker sẽ chạy ngay khi ứng dụng Start
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _services.CreateScope())
                {
                    // Lấy EventReminderService ra để sử dụng
                    var reminderService = scope.ServiceProvider.GetRequiredService<EventReminderService>();

                    // Gọi hàm gửi mail
                    await reminderService.SendWeeklyRemindersAsync();
                }

                // Chờ 24 giờ sau mới kiểm tra và gửi tiếp (tránh gửi liên tục)
                // Bạn có thể chỉnh lại thời gian chờ tùy ý
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}