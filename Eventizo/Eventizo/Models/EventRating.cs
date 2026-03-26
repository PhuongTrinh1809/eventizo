using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class EventRating
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }
        public string UserName { get; set; } // <-- thêm

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 🔹 Thêm liên kết User
        [Required]
        public string UserId { get; set; }   // Id của ApplicationUser
        public DateTime? UpdatedAt { get; set; }
        public ApplicationUser? User { get; set; }

        [ForeignKey(nameof(EventId))]
        public Event? Event { get; set; }
    }
}
