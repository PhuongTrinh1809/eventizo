using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;

namespace Eventizo.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        // 🟢 Tên sự kiện
        [Required(ErrorMessage = "Tên sự kiện là bắt buộc")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        // 📝 Mô tả sự kiện
        public string? Description { get; set; }

        // 📅 Thời gian diễn ra
        [Required(ErrorMessage = "Ngày diễn ra là bắt buộc")]
        public DateTime EventStartingDate { get; set; }
        public DateTime EventEndingDate { get; set; }

        // 📍 Địa điểm
        [Required(ErrorMessage = "Vui lòng nhập địa điểm tổ chức")]
        [StringLength(200)]
        public string Place { get; set; } = string.Empty;

        // ⚙️ Trạng thái sự kiện
        public string? Status { get; set; } = "Đang mở";

        // 👥 Sức chứa
        [Range(1, 100000, ErrorMessage = "Sức chứa phải lớn hơn 0")]
        public int Capacity { get; set; }

        // 🖼️ Ảnh đại diện
        public string? ImageUrl { get; set; }

        // 🖼️ Ảnh chi tiết (1-n)
        public List<EventImage>? Images { get; set; }

        // 🔗 Loại sự kiện (FK)
        [ForeignKey("EventType")]
        public int EventTypeId { get; set; }
        public EventType? EventType { get; set; }

        // 🎟️ Vé (1-n)
        public List<Ticket>? Tickets { get; set; }

        // 🌟 Đánh giá (1-n)
        public List<EventRating>? Ratings { get; set; } = new List<EventRating>();

        // ❤️ Bình chọn yêu thích (1-n)
        public List<FavoriteVote>? FavoriteVotes { get; set; } = new List<FavoriteVote>();

        // ⭐ Điểm trung bình (tự động tính)
        [Column(TypeName = "decimal(3,2)")]
        [DefaultValue(0)]
        public decimal AverageRating { get; set; } = 0;

        // 💰 Giá vé
        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 đến 100,000,000 VND")]
        public decimal PriceMin { get; set; }

        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 đến 100,000,000 VND")]
        [BindProperty]
        public decimal? PriceReducedMin { get; set; }

        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 đến 100,000,000 VND")]
        public decimal PriceMax { get; set; }

        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 đến 100,000,000 VND")]
        [BindProperty]
        public decimal? PriceReducedMax { get; set; }

        // 🎫 Các loại vé (VIP, Standard...)
        public string? TicketType { get; set; } = string.Empty;

        // ⚙️ Chuyển đổi list vé ↔ chuỗi để hiển thị/gửi dữ liệu dễ hơn
        [NotMapped]
        public List<string> TicketList
        {
            get => string.IsNullOrEmpty(TicketType)
                    ? new List<string>()
                    : TicketType.Split(',').ToList();

            set => TicketType = value != null
                    ? string.Join(",", value)
                    : string.Empty;
        }

        public List<Product> Products { get; set; } = new List<Product>();
    }
}
