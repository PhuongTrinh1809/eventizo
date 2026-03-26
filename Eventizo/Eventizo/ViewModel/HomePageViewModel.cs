using Eventizo.Models;

namespace Eventizo.ViewModel
{
    public class HomePageViewModel
    {
        public List<EventRevenue> UpcomingEvents { get; set; }
        public List<EventRevenue> TopRevenueEvents { get; set; }
        public List<EventRevenue> CurrentMonthEvents { get; set; }
        public List<EventRevenue> BestSellingEvents { get; set; }
    }
}
