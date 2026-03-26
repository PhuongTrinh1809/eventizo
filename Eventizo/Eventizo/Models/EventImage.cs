namespace Eventizo.Models
{
    public class EventImage
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public int EventId { get; set; }
        public Event? Event { get; set; }
    }
}
