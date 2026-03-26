namespace Eventizo.Models
{
    public class BookingRequest
    {
        public int EventId { get; set; }
        public List<string> Seats { get; set; }
    }
}
