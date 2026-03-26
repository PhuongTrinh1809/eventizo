namespace Eventizo.Models
{
    public class PayOSWebhookData
    {
        public long orderCode { get; set; }
        public int amount { get; set; }
        public string description { get; set; }
        public string transactionDateTime { get; set; }
        public string accountNumber { get; set; }
        public string virtualAccountNumber { get; set; }
    }
}
