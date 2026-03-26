using System;
using System.ComponentModel.DataAnnotations;

namespace Eventizo.Models
{
    public class UserPoint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int Points { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
