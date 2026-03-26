using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class UserPointHistory
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // liên kết ApplicationUser

        public int Points { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }
    }
}
