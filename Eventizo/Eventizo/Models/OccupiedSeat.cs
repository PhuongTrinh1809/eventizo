namespace Eventizo.Models
{
    public class OccupiedSeat
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string SeatCode { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
