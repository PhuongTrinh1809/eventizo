using Eventizo.Data;
using Eventizo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Net.payOS;
using Net.payOS.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eventizo.Controllers
{
    public class PaymentController : Controller
    {   
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan pendingSeatTimeout = TimeSpan.FromMinutes(15);

        public PaymentController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ======================= GET =======================
        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            var eventInfo = await _context.Events.FindAsync(id);
            if (eventInfo == null)
            {
                TempData["Error"] = "Không tìm thấy sự kiện.";
                return RedirectToAction("Index", "Home");
            }

            return View(new Payment
            {
                EventId = id,
                Event = eventInfo
            });
        }

        // ======================= POST =======================
        [HttpPost]
        public async Task<IActionResult> Index(int id, string selectedSeats, string seatTypes)
        {
            var eventInfo = await _context.Events.FindAsync(id);
            if (eventInfo == null)
            {
                TempData["Error"] = "Không tìm thấy sự kiện.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(selectedSeats) || string.IsNullOrEmpty(seatTypes))
            {
                TempData["Error"] = "Vui lòng chọn ghế.";
                return RedirectToAction("Index", "Home");
            }

            var seatList = selectedSeats
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            var typeList = seatTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            if (typeList.Length == 1 && seatList.Length > 1)
                typeList = Enumerable.Repeat(typeList[0], seatList.Length).ToArray();

            if (seatList.Length != typeList.Length)
            {
                TempData["Error"] = "Dữ liệu ghế không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Vui lòng đăng nhập.";
                return RedirectToAction("Login", "Account");
            }

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // ================= TRANSACTION =================
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                // 1️⃣ Xóa pending quá hạn
                var expiration = DateTime.Now - pendingSeatTimeout;
                var expired = await _context.PendingPaymentSeats
                    .Where(x => x.CreatedAt < expiration)
                    .ToListAsync();

                if (expired.Any())
                {
                    _context.PendingPaymentSeats.RemoveRange(expired);
                    await _context.SaveChangesAsync();
                }

                // 2️⃣ Kiểm tra trùng ghế (LOCK)
                foreach (var seat in seatList)
                {
                    bool occupied = await _context.OccupiedSeats
                        .AnyAsync(x => x.EventId == id && x.SeatCode == seat);

                    bool pending = await _context.PendingPaymentSeats
                        .AnyAsync(x => x.EventId == id && x.SeatCode == seat);

                    if (occupied || pending)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Ghế {seat} đã được người khác chọn.";
                        return RedirectToAction("Index", "Home");
                    }
                }

                // 3️⃣ Lưu ghế pending
                foreach (var seat in seatList)
                {
                    _context.PendingPaymentSeats.Add(new PendingPaymentSeat
                    {
                        EventId = id,
                        SeatCode = seat,
                        UserId = userId,
                        OrderCode = orderCode,
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Ghế vừa được người khác giữ. Vui lòng thử lại.";
                return RedirectToAction("Index", "Home");
            }

            // ================= TÍNH GIÁ =================
            decimal totalAmount = 0;
            var seatPrices = new List<decimal>();

            for (int i = 0; i < seatList.Length; i++)
            {
                decimal price = typeList[i].Equals("VIP", StringComparison.OrdinalIgnoreCase)
                    ? (eventInfo.PriceReducedMax ?? eventInfo.PriceMax)
                    : (eventInfo.PriceReducedMin ?? eventInfo.PriceMin);

                seatPrices.Add(price);
                totalAmount += price;
            }

            // ================= PAYOS =================
            try
            {
                var payOS = new PayOS(
                    _configuration["PayOS:ClientId"],
                    _configuration["PayOS:ApiKey"],
                    _configuration["PayOS:ChecksumKey"]
                );

                var items = seatList.Select((s, i) =>
                    new ItemData($"{s} - {typeList[i]}", 1, (int)seatPrices[i])
                ).ToList();

                var paymentData = new PaymentData(
                    orderCode,
                    (int)totalAmount,
                    $"{eventInfo.Name} - Ghế {string.Join(",", seatList)}",
                    items,
                    $"{_configuration["PayOS:ReturnUrl"]}?id={id}&orderCode={orderCode}",
                    $"{_configuration["PayOS:CancelUrl"]}?id={id}&orderCode={orderCode}"
                );

                var response = await payOS.createPaymentLink(paymentData);

                _context.Payments.Add(new Payment
                {
                    OrderCode = orderCode,
                    Status = "PENDING",
                    EventId = id,
                    UserId = userId,
                    SelectedSeats = selectedSeats,
                    TicketType = seatTypes,
                    SeatPrices = string.Join(";", seatPrices),
                    TotalAmount = totalAmount,
                    Quantity = seatList.Length,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Redirect(response.checkoutUrl);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi PayOS: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // ================= API GHẾ =================
        [HttpGet]
        public async Task<IActionResult> GetOccupiedSeats(int eventId)
        {
            var occupied = await _context.OccupiedSeats
                .Where(x => x.EventId == eventId)
                .Select(x => x.SeatCode)
                .ToListAsync();

            var pending = await _context.PendingPaymentSeats
                .Where(x => x.EventId == eventId)
                .Select(x => x.SeatCode)
                .ToListAsync();

            return Json(new
            {
                occupiedSeats = occupied,
                pendingSeats = pending
            });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Success(int id, string orderCode)
        {
            if (!long.TryParse(orderCode, out var code))
            {
                TempData["Error"] = "Mã đơn hàng không hợp lệ!";
                return RedirectToAction("Index", "Home");
            }

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == code);
            if (payment == null || payment.Status != "SUCCESS")
            {
                TempData["Error"] = "Thanh toán đang chờ xác nhận.";
                return RedirectToAction("Index", "Home");
            }

            TempData["SuccessMessage"] = $"Thanh toán thành công! Mã đơn hàng: {orderCode}.";
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public async Task<IActionResult> Cancel(int id, string orderCode)
        {
            if (!string.IsNullOrEmpty(orderCode) && long.TryParse(orderCode, out var code))
            {
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.OrderCode == code && p.Status == "PENDING");

                if (payment != null)
                {
                    payment.Status = "CANCELLED";

                    var pendingSeats = await _context.PendingPaymentSeats
                        .Where(x => x.OrderCode == code)
                        .ToListAsync();

                    _context.PendingPaymentSeats.RemoveRange(pendingSeats);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Message"] = "Thanh toán bị hủy.";
            return RedirectToAction("Index", "Home");
        }
    }
}
