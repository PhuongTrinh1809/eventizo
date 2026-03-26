using System;
using System.ComponentModel.DataAnnotations;

namespace Eventizo.Models
{
    public class Payment
    {
        public int Id { get; set; } // ID tự tăng của cơ sở dữ liệu

        // -------------------------------------------------
        // 🔹 TRƯỜNG BẮT BUỘC CHO PAYOS
        // -------------------------------------------------

        [Required]
        public long OrderCode { get; set; } // Mã đơn hàng duy nhất gửi cho PayOS

        [Required]
        public string Status { get; set; } // "Pending", "PAID", "FAILED", "CANCELLED"

        // -------------------------------------------------
        // 🔹 CÁC TRƯỜNG CỦA BẠN (CÓ TINH CHỈNH)
        // -------------------------------------------------
        public string UserId { get; set; }

        [Required]
        public int Quantity { get; set; }

        public string SelectedSeats { get; set; }
        public string SeatPrices { get; set; }
        public decimal TotalAmount { get; set; }
        public int EventId { get; set; }

        public Event Event { get; set; }
        public string TicketType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời điểm tạo đơn hàng

        // Sửa: Nên là nullable, vì chỉ khi "PAID" mới có ngày thanh toán
        public DateTime? PaymentDate { get; set; }
    }
}