using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Net.payOS;
using Net.payOS.Types;

namespace Eventizo.Services
{
    public class PayOSService
    {
        private readonly IConfiguration _config;

        public PayOSService(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // Tạo link thanh toán PayOS với SDK
        public async Task<string> CreatePaymentLinkAsync(
            List<decimal> prices,
            string eventName,
            string returnUrl,
            string cancelUrl)
        {
            if (prices == null || prices.Count == 0)
                throw new ArgumentException("Danh sách giá vé không được trống");

            string clientId = _config["PayOS:ClientId"];
            string apiKey = _config["PayOS:ApiKey"];
            string checksumKey = _config["PayOS:ChecksumKey"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(checksumKey))
                throw new Exception("ClientId, ApiKey hoặc ChecksumKey chưa cấu hình");

            // 🔹 Khởi tạo SDK PayOS
            var payOS = new PayOS(clientId, apiKey, checksumKey);

            // 🔹 Tạo orderCode số dương
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 🔹 Giới hạn description <= 25 ký tự
            string Name = eventName.Length > 25 ? eventName.Substring(0, 25) : eventName;

            // 🔹 Tạo list items với name <= 25 ký tự
            var items = prices.Select(p => new ItemData(
                name: Name,
                quantity: 1,
                price: (int)Math.Round(p, 0)
            )).ToList();

            // 🔹 Chuẩn bị PaymentData gửi SDK
            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)prices.Sum(),
                description: Name,
                returnUrl: returnUrl,
                cancelUrl: cancelUrl,
                items: items
            );

            // 🔹 Gọi SDK để tạo link
            var paymentLink = await payOS.createPaymentLink(paymentData);

            return paymentLink.checkoutUrl;
        }
    }
}
