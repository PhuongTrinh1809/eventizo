using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class ChatHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public string EncryptedUserMessage { get; set; } = string.Empty;

        [Required]
        public string EncryptedBotReply { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // (Optional) Nếu muốn liên kết với tài khoản
        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }
    }
}
