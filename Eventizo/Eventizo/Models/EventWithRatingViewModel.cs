using System;
using System.Collections.Generic;
using System.Linq;
using Eventizo.Models;

namespace Eventizo.Models.ViewModels
{
    public class EventWithRatingsViewModel
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string Place { get; set; } = "";
        public DateTime EventDate { get; set; }
        public int FavoriteVotes { get; set; }          // Tổng lượt bình chọn
        public bool IsUserVoted { get; set; } = false;  // Người dùng hiện tại đã bình chọn chưa
        public bool HasRated { get; set; } // đánh giá 
        public DateTime Start { get; set; } // 👈 hoặc tên khác, không phải StartDate
        public DateTime End { get; set; }
        public List<EventRating> Ratings { get; set; } = new();

        public double AverageRating => Ratings.Any()
            ? Math.Round(Ratings.Average(r => r.Rating), 1)
            : 0;

        public int TotalRatings => Ratings?.Count ?? 0;
    }
}
