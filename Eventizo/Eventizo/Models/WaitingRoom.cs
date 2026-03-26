using System.ComponentModel.DataAnnotations;

namespace Eventizo.Models
{
    public class WaitingRoom
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; }
        public int Position { get; set; }
        public DateTime JoinTime { get; set; }
        public DateTime LastActive { get; set; }
    }

}
