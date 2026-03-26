using System;
using System.ComponentModel.DataAnnotations;

namespace Eventizo.Models
{
    public class FavoriteVote
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        public string UserIdentifier { get; set; } = ""; // Có thể là UserId hoặc IP

        public DateTime CreatedAt { get; set; }

        public Event Event { get; set; } = null!;
    }
}
