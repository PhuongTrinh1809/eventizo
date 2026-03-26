namespace Eventizo.Models
{
    public class PayOSWebhook
    {
        public string code { get; set; }
        public string desc { get; set; }
        public bool success { get; set; }
        public PayOSWebhookData data { get; set; }
        public string signature { get; set; }
    }
}
