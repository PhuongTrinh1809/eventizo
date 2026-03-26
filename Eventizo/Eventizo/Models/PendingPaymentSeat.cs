namespace Eventizo.Models
{
    public class PendingPaymentSeat
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string SeatCode { get; set; }
        public string UserId { get; set; }
        public long OrderCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
