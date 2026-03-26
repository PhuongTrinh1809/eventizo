using Eventizo.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        public string Role { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}