using System;
using System.Linq;
using Eventizo.Data;
using Eventizo.Models;

namespace Eventizo.Services
{
    public class PointService
    {
        private readonly ApplicationDbContext _context;

        public PointService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ CỘNG ĐIỂM (có giới hạn tuần 300 điểm)
        public void AddPoints(string userId, int points, string? description = "Điểm thưởng hoạt động")
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return;

            // Tính tổng điểm trong 7 ngày gần nhất
            var weekAgo = DateTime.Now.AddDays(-7);
            var totalThisWeek = _context.UserPointHistories
                .Where(p => p.UserId == userId && p.CreatedAt >= weekAgo)
                .Sum(p => p.Points);

            // Giới hạn 300 điểm/tuần
            if (totalThisWeek >= 300)
                return;

            int actualAdd = Math.Min(points, 300 - totalThisWeek);

            // Lưu lịch sử cộng điểm
            _context.UserPointHistories.Add(new UserPointHistory
            {
                UserId = userId,
                Points = actualAdd,
                CreatedAt = DateTime.Now,
                Description = description
            });

            user.Points += actualAdd;
            UpdateLevel(user);
            _context.SaveChanges();
        }

        // ✅ CỘNG ĐIỂM KHI MUA VÉ
        public void AddPointsForTicket(string userId, bool isVipTicket)
        {
            int points = isVipTicket ? 50 : 20; // VIP nhiều hơn vé thường
            AddPoints(userId, points, isVipTicket ? "Điểm mua vé VIP" : "Điểm mua vé thường");
        }

        // ✅ TRỪ ĐIỂM
        public void DeductPoints(string userId, int points)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return;

            user.Points = Math.Max(0, user.Points - points);
            UpdateLevel(user);
            _context.SaveChanges();
        }

        // ✅ Cập nhật cấp hạng
        public void UpdateLevel(ApplicationUser user)
        {
            var daysSinceCreated = (DateTime.Now - user.CreatedDate).TotalDays;

            // Sau 30 ngày mà chưa đủ 50 điểm -> hạng "Thành viên"
            if (daysSinceCreated >= 30 && user.Points < 50)
            {
                user.MemberLevel = "👤 Thành viên";
                return;
            }

            user.MemberLevel = user.Points switch
            {
                >= 1000 => "👑 Hạng VIP",
                >= 500 => "🏆 Hạng Vàng",
                >= 200 => "🥈 Hạng Bạc",
                >= 50 => "👤 Thành viên",
                _ => "🌱 Thành viên mới"
            };
        }

        // ✅ Mốc thăng hạng
        public int GetNextLevelTarget(int points)
        {
            if (points < 50) return 50;
            if (points < 200) return 200;
            if (points < 500) return 500;
            if (points < 1000) return 1000;
            return points;
        }

        // ✅ Tính % tiến trình
        public double GetProgressPercentage(int points)
        {
            int baseLevel = GetCurrentLevelBase(points);
            int nextLevel = GetNextLevelTarget(points);
            if (baseLevel == nextLevel) return 100;
            return Math.Min(100, (double)(points - baseLevel) / (nextLevel - baseLevel) * 100);
        }

        private int GetCurrentLevelBase(int points)
        {
            if (points >= 1000) return 1000;
            if (points >= 500) return 500;
            if (points >= 200) return 200;
            if (points >= 50) return 50;
            return 0;
        }

        public int GetTotalPoints(string userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            return user?.Points ?? 0;
        }
    }
}
