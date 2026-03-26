using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SeatNumber { get; set; } = string.Empty;

        public DateTime BookedAt { get; set; } = DateTime.Now;

        // ------------------- Liên kết sự kiện -------------------
        [ForeignKey(nameof(Event))]
        public int EventId { get; set; }
        public Event Event { get; set; }

        // ------------------- Người hiện tại sở hữu vé -------------------
        [ForeignKey(nameof(User))]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Chờ xác nhận";

        public string? Phone { get; set; }
        public string? PaymentMethod { get; set; }

        public string? TicketType { get; set; }

        // ------------------- Blockchain -------------------
        [Required]
        public string QRHash { get; set; } = string.Empty;

        public string? PreviousHash { get; set; }

        // ------------------- Chuyển nhượng -------------------
        public bool IsTransferred { get; set; } = false;

        public DateTime? TransferredAt { get; set; }

        public DateTime? TransferRequestedAt { get; set; } // thời điểm gửi yêu cầu chuyển nhượng

        public string? NewOwnerId { get; set; }

        [ForeignKey(nameof(NewOwnerId))]
        public ApplicationUser? NewOwner { get; set; } // ✅ Người nhận

        public string? PreviousOwnerId { get; set; }

        [ForeignKey(nameof(PreviousOwnerId))]
        public ApplicationUser? PreviousOwner { get; set; } // ✅ Người chuyển

        public bool IsVisibleToPreviousOwner { get; set; } = true;

        // ✅ Trạng thái chuyển nhượng (Pending, Completed, Cancelled)
        [MaxLength(20)]
        public string TransferStatus { get; set; } = "None";

        // ✅ Người nhận có xác nhận chưa
        public bool? IsTransferAccepted { get; set; } = null;

        // ✅ Ghi chú chuyển nhượng (ai chuyển cho ai)
        public string? TransferNote { get; set; }
        [Required]
        public long OrderCode { get; set; }
        public long? TransferOrderCode { get; set; }

        // ------------------- Bank -------------------
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountOwner { get; set; }

        // ------------------- QR Code Base64 -------------------
        [NotMapped]
        public string? QRImage { get; set; }
        // ------------------- Check-in -------------------
        public bool IsCheckedIn { get; set; } = false;
        public DateTime? CheckedInAt { get; set; }
    }
}
