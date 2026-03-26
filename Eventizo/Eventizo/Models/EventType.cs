namespace Eventizo.Models
{
    public class EventType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Event>? Events { get; set; }
    }
}
