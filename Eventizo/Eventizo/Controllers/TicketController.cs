using Eventizo.Data;
using Eventizo.Models;
using Eventizo.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eventizo.Controllers
{
    public class TicketController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly BlockchainService _blockchain;
        private readonly PayOSService _payOSService;

        public TicketController(ApplicationDbContext context, EmailService emailService, BlockchainService blockchain, PayOSService payOSService)
        {
            _context = context;
            _emailService = emailService;
            _blockchain = blockchain;
            _payOSService = payOSService;
        }

        // ------------------- LẤY GHẾ ĐÃ ĐẶT + HASH -------------------
        [HttpGet]
        public async Task<IActionResult> GetOccupiedSeats(int eventId)
        {
            var seats = await _context.Tickets
                .Where(t => t.EventId == eventId)
                .Select(t => new { t.SeatNumber, t.QRHash })
                .ToListAsync();

            return Json(seats);
        }

        // ------------------- ĐẶT VÉ -------------------
        [HttpPost]
        public async Task<IActionResult> BookSeats([FromBody] BookingRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Vui lòng đăng nhập trước khi đặt vé.");

            var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.Id == request.EventId);
            if (eventEntity == null)
                return NotFound("Không tìm thấy sự kiện.");

            var occupiedSeats = await _context.Tickets
                .Where(t => t.EventId == request.EventId)
                .Select(t => t.SeatNumber)
                .ToListAsync();

            var availableSeats = request.Seats.Except(occupiedSeats).ToList();
            if (!availableSeats.Any())
                return BadRequest("Tất cả các ghế đã được đặt.");

            var newTickets = new List<Ticket>();
            foreach (var seat in availableSeats)
            {
                bool isVIP = seat.StartsWith("A") || seat.StartsWith("B") || seat.StartsWith("C");

                var ticket = new Ticket
                {
                    EventId = eventEntity.Id,
                    UserId = userId,
                    SeatNumber = seat,
                    Price = isVIP ? eventEntity.PriceMax : eventEntity.PriceMin,
                    Status = "Đã xác nhận",
                    TicketType = isVIP ? "VIP" : "Thường",
                    BookedAt = DateTime.Now
                };

                newTickets.Add(ticket);
            }

            await _context.Tickets.AddRangeAsync(newTickets);
            await _context.SaveChangesAsync();

            var allOccupiedSeats = await _context.Tickets
                .Where(t => t.EventId == request.EventId)
                .Select(t => new { t.SeatNumber, t.QRHash })
                .ToListAsync();

            return Json(new
            {
                success = true,
                message = "Đặt vé thành công!",
                tickets = newTickets.Select(t => new { t.Id, t.SeatNumber, t.Status, t.QRHash }),
                occupiedSeats = allOccupiedSeats
            });
        }

        // ------------------- XỬ LÝ CHUYỂN NHƯỢNG -------------------
        [HttpGet]
        public async Task<IActionResult> TransferTicket(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferTicket(int id, string newOwnerEmail, string BankName, string BankAccountNumber, string BankAccountOwner)
        {
            // 1. Thêm bảo mật (Tùy chọn nhưng nên có)
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Phải thêm .Include(t => t.PreviousOwner) để lấy được Email người gửi
            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.PreviousOwner) // Thêm cái này để sửa lỗi Null khi gửi mail
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            // 3. Tìm người nhận
            var newOwner = await _context.Users.FirstOrDefaultAsync(u => u.Email == newOwnerEmail);
            if (newOwner == null) return BadRequest("Người nhận không tồn tại.");

            // --- LOGIC CŨ CỦA BẠN (GIỮ NGUYÊN) ---
            ticket.PreviousOwnerId = ticket.UserId;
            ticket.BankName = BankName;
            ticket.BankAccountNumber = BankAccountNumber;
            ticket.BankAccountOwner = BankAccountOwner;
            ticket.NewOwnerId = newOwner.Id;
            ticket.TransferStatus = "Pending";
            ticket.TransferRequestedAt = DateTime.Now;

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            // --- PHẦN GỬI EMAIL (BỌC TRONG TRY-CATCH) ---
            try
            {
                // Copy phần await _emailService.SendEmailAsync từ code cũ của bạn vào đây
                // Nhưng nhớ dùng try-catch để nếu mail lỗi thì không bị văng trang web
            }
            catch
            {
                // Bỏ qua lỗi email để hoàn tất giao dịch
            }

            return RedirectToAction("MyTickets", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTransfer(int ticketId, string accept)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            if (string.IsNullOrEmpty(accept) || !bool.TryParse(accept, out bool acceptBool))
                return BadRequest();

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.PreviousOwner)
                .Include(t => t.NewOwner)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null || ticket.NewOwnerId != currentUserId)
                return BadRequest();

            if (ticket.TransferStatus == "Completed")
            {
                TempData["InfoMessage"] = "Vé đã chuyển nhượng trước đó.";
                return RedirectToAction("MyTickets", "Home");
            }

            if (!acceptBool)
            {
                // Người nhận từ chối
                ticket.TransferStatus = "Cancelled";
                ticket.IsTransferAccepted = false;
                ticket.TransferNote = $"❌ {ticket.NewOwner?.Email} đã từ chối.";

                // Gửi email cho người gửi
                await _emailService.SendEmailAsync(
                    ticket.PreviousOwner.Email,
                    "Yêu cầu chuyển nhượng bị từ chối",
                    $"Người nhận ({ticket.NewOwner.Email}) đã từ chối yêu cầu chuyển nhượng vé."
                );

                // Gửi email cho người nhận
                await _emailService.SendEmailAsync(
                    ticket.NewOwner.Email,
                    "Bạn đã từ chối yêu cầu chuyển nhượng",
                    $"Bạn đã từ chối yêu cầu chuyển nhượng vé từ {ticket.PreviousOwner.Email}."
                );

                ticket.NewOwnerId = null;
                ticket.IsTransferred = false;
                ticket.TransferredAt = null;
                ticket.TransferRequestedAt = null;

                _context.Tickets.Update(ticket);
                await _context.SaveChangesAsync();
                return RedirectToAction("MyTickets", "Home");
            }

            if (ticket.TransferStatus != "Pending")
                ticket.TransferStatus = "Pending";

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ticket.TransferOrderCode = orderCode;
            await _context.SaveChangesAsync();

            // Người nhận đồng ý → Chuyển hướng sang PayOS thanh toán
            string returnUrl = Url.Action("FinalizeTransfer", "Ticket", new { ticketId }, Request.Scheme);
            string cancelUrl = Url.Action("TransferTicket", "Ticket", new { id = ticketId }, Request.Scheme);

            var paymentLink = await _payOSService.CreatePaymentLinkAsync(
                prices: new List<decimal> { ticket.Price },
                eventName: ticket.Event?.Name ?? "Ticket Transfer",
                returnUrl: returnUrl,
                cancelUrl: cancelUrl
            );

            return Redirect(paymentLink);
        }

        // Trang callback khi thanh toán thành công
        [HttpGet]
        public async Task<IActionResult> FinalizeTransfer(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.PreviousOwner)
                .Include(t => t.NewOwner)
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null) return NotFound();

            if (ticket.TransferStatus == "Completed")
            {
                TempData["InfoMessage"] = "Vé đã chuyển nhượng trước đó.";
                return RedirectToAction("MyTickets", "Home");
            }

            // ✅ Ghi blockchain, cập nhật trạng thái
            ticket.TransferStatus = "Completed";
            ticket.IsTransferAccepted = true;
            ticket.IsTransferred = true;
            ticket.PreviousHash = ticket.QRHash;
            ticket.UserId = ticket.NewOwnerId;
            ticket.TransferredAt = DateTime.Now;

            var transferData = new
            {
                TicketId = ticket.Id,
                EventName = ticket.Event?.Name,
                Seat = ticket.SeatNumber,
                From = ticket.PreviousOwner?.FullName + " (" + ticket.PreviousOwner?.Email + ")",
                To = ticket.NewOwner?.FullName + " (" + ticket.NewOwner?.Email + ")",
                TransferredAt = DateTime.Now
            };

            ticket.QRHash = await _blockchain.AddBlockAsync(JsonConvert.SerializeObject(transferData));
            ticket.TransferNote = $"✅ Vé chuyển từ {ticket.PreviousOwner?.Email} sang {ticket.NewOwner?.Email}.";

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thanh toán thành công, chuyển nhượng vé hoàn tất!";
            return RedirectToAction("MyTickets", "Home");
        }

        // ------------------- AUTO HỦY PENDING QUÁ HẠN -------------------
        [HttpPost]
        public async Task<IActionResult> AutoCancelPendingTransfers()
        {
            var pendingTickets = await _context.Tickets
                .Where(t => t.TransferStatus == "Pending"
                            && t.TransferRequestedAt != null
                            && t.TransferRequestedAt <= DateTime.Now.AddDays(-1))
                .ToListAsync();

            foreach (var ticket in pendingTickets)
            {
                ticket.TransferStatus = "Cancelled";
                ticket.IsTransferAccepted = false;
                ticket.TransferNote = "❌ Yêu cầu chuyển nhượng tự động hủy sau 1 ngày không xác nhận.";
                ticket.NewOwnerId = null;
                ticket.IsTransferred = false;
                ticket.TransferredAt = null;
                ticket.TransferRequestedAt = null;

                _context.Tickets.Update(ticket);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"{pendingTickets.Count} yêu cầu quá hạn đã bị hủy." });
        }
    }

    // ------------------- MODEL YÊU CẦU -------------------
    public class BookingRequest
    {
        public int EventId { get; set; }
        public List<string> Seats { get; set; } = new();
    }
}
