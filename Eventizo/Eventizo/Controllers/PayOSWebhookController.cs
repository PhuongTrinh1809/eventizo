using Eventizo.Data;
using Eventizo.Models;
using Eventizo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eventizo.Controllers
{
    [Route("api/payos")]
    [ApiController]
    public class PayOSWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly BlockchainService _blockchain;

        public PayOSWebhookController(ApplicationDbContext context, BlockchainService blockchain)
        {
            _context = context;
            _blockchain = blockchain;
        }

        [HttpPost("Webhook")]
        public async Task<IActionResult> Webhook()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            PayOSWebhook payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<PayOSWebhook>(rawBody);
            }
            catch
            {
                return BadRequest("Invalid payload");
            }

            if (payload?.data == null)
                return BadRequest("NO DATA");

            long orderCode = payload.data.orderCode;
            bool isSuccess = payload.success && payload.code == "00";

            // --- Xử lý Payment vé ---
            var payment = await _context.Payments.FirstOrDefaultAsync(x => x.OrderCode == orderCode);
            if (payment != null)
            {
                var pendingSeats = await _context.PendingPaymentSeats
                    .Where(x => x.OrderCode == orderCode)
                    .ToListAsync();

                if (isSuccess)
                {
                    payment.Status = "SUCCESS";
                    payment.PaymentDate = DateTime.Now;

                    // Lấy thông tin user
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == payment.UserId);

                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        string subject = "Xác nhận thanh toán vé thành công";
                        string body = $@"
                            <h2>Thanh toán thành công!</h2>
                            <p><b>Sự kiện:</b> {payment.EventId}</p>
                            <p><b>Mã đơn hàng:</b> {payment.OrderCode}</p>
                            <p><b>Ghế:</b> {payment.SelectedSeats}</p>
                            <p><b>Loại vé:</b> {payment.TicketType}</p>
                            <p><b>Tổng tiền:</b> {payment.TotalAmount:N0} VND</p>
                            <p>Cảm ơn bạn đã đặt vé tại Eventizo!</p>
                        ";

                        await Eventizo.Helper.EmailHelper.SendEmailAsync(
                            user.Email,
                            subject,
                            body
                        );
                    }

                    // Chuyển ghế sang Occupied
                    foreach (var seat in pendingSeats)
                    {
                        _context.OccupiedSeats.Add(new OccupiedSeat
                        {
                            EventId = seat.EventId,
                            SeatCode = seat.SeatCode,
                            UserId = seat.UserId,
                            CreatedAt = DateTime.Now
                        });
                    }
                    _context.PendingPaymentSeats.RemoveRange(pendingSeats);

                    // Tạo vé
                    var seats = payment.SelectedSeats.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var types = payment.TicketType.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var prices = payment.SeatPrices.Split(';');

                    var existed = await _context.Tickets
                        .Where(t => t.OrderCode == orderCode)
                        .ToListAsync();

                    if (!existed.Any())
                    {
                        var ticketsToAdd = new List<Ticket>();
                        for (int i = 0; i < seats.Length; i++)
                        {
                            decimal price = decimal.Parse(prices[i].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                            ticketsToAdd.Add(new Ticket
                            {
                                EventId = payment.EventId,
                                UserId = payment.UserId,
                                OrderCode = payment.OrderCode,
                                SeatNumber = seats[i],
                                TicketType = types[i],
                                Price = price,
                                Status = "Đã xác nhận",
                                BookedAt = DateTime.Now
                            });
                        }

                        _context.Tickets.AddRange(ticketsToAdd);
                        await _context.SaveChangesAsync();

                        // Blockchain + QRHash
                        foreach (var ticket in ticketsToAdd)
                        {
                            string previousHash = ticket.QRHash; // lưu QRHash cũ nếu muốn
                            string jsonData = JsonSerializer.Serialize(ticket);
                            ticket.QRHash = await _blockchain.AddBlockAsync(jsonData);
                            ticket.PreviousHash = previousHash;
                        }

                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    payment.Status = "CANCELLED";
                    _context.PendingPaymentSeats.RemoveRange(pendingSeats);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                // --- Xử lý Payment2 (sản phẩm) ---
                var payment2 = await _context.Payment2s.FirstOrDefaultAsync(p => p.OrderCode == orderCode);
                if (payment2 != null)
                {
                    if (isSuccess)
                    {
                        payment2.OrderStatus = "SUCCESS";
                        payment2.PaymentDate = DateTime.Now;

                        // Lấy thông tin user
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == payment2.UserId);

                        // Lấy thông tin sản phẩm
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == payment2.ProductId);

                        if (user != null && product != null)
                        {
                            string subject = "Xác nhận thanh toán sản phẩm thành công";
                            string body = $@"
                             <h2>Thanh toán thành công!</h2>
                             <p><b>Sản phẩm:</b> {product.Name}</p>
                             <p><b>Mã đơn hàng:</b> {payment2.OrderCode}</p>
                             <p><b>Số lượng:</b> {payment2.Quantity}</p>
                             <p><b>Tổng tiền:</b> {payment2.TotalAmount:N0} VND</p>
                             <br/>
                             <p>Cảm ơn bạn đã mua sản phẩm tại Eventizo!</p> ";

                            await Eventizo.Helper.EmailHelper.SendEmailAsync(
                                user.Email,
                                subject,
                                body
                            );
                        }

                    }
                    else
                    {
                        payment2.OrderStatus = "CANCELLED";
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    return NotFound("Payment not found");
                }
            }

            return Ok(new { message = "OK" });
        }
    }
}
