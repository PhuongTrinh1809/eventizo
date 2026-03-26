using System.Collections.Generic;

namespace Eventizo.Models
{
    public class MyTicketsViewModel
    {
        public List<Ticket> MyTickets { get; set; } = new();
        public List<Ticket> IncomingTransfers { get; set; } = new();
    }
}