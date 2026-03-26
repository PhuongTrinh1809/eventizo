using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation? Conversation { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [ForeignKey(nameof(SenderId))]
        public ApplicationUser? Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        [ForeignKey(nameof(ReceiverId))]
        public ApplicationUser? Receiver { get; set; }

        [Required]
        [MaxLength(200)]
        public string SenderName { get; set; } = string.Empty;

        [NotMapped] 
        public string MessageText { get; set; } = string.Empty;

        [Required]
        public string EncryptedMessage { get; set; } = string.Empty;

        public string? Content { get; set; }

        public DateTime SentAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsRead { get; set; } = false;

        public string? ImageUrl { get; set; } // 🔹 Link ảnh được gửi

        public ChatMessage()
        {
            SentAt = DateTime.UtcNow.AddHours(7);
            CreatedAt = DateTime.UtcNow.AddHours(7);
        }
    }
}