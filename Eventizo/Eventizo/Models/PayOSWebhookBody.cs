namespace Eventizo.Models
{
    public class PayOSWebhookBody
    {
        public long orderCode { get; set; }
        public string status { get; set; }
        public int amount { get; set; }
        public string description { get; set; }
        public string transactionDateTime { get; set; }
    }
}
