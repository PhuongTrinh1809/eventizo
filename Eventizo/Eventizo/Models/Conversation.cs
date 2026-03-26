using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [ForeignKey(nameof(CustomerId))]
        public ApplicationUser? Customer { get; set; }

        public string? AdminId { get; set; }

        [ForeignKey(nameof(AdminId))]
        public ApplicationUser? Admin { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public ICollection<ChatMessage>? Messages { get; set; }
    }
}