using Eventizo.Data;
using Eventizo.Models;
using Eventizo.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class SuperAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly BlockchainService _blockchain;

        public SuperAdminController(ApplicationDbContext context, IWebHostEnvironment env, BlockchainService blockchain)
        {
            _context = context;
            _env = env;
            _blockchain = blockchain;
        }

        // ------------------------ Helper xử lý ticket & blockchain ------------------------
        private async Task<bool> ProcessTicketConfirmationAndBlockchain(Ticket ticket)
        {
            if (ticket.Status == "Đã xác nhận" && !string.IsNullOrEmpty(ticket.QRHash))
                return true;

            await _context.Entry(ticket).Reference(t => t.Event).LoadAsync();
            await _context.Entry(ticket.Event).Reference(e => e.EventType).LoadAsync();
            await _context.Entry(ticket).Reference(t => t.User).LoadAsync();

            ticket.Status = "Đã xác nhận";

            var ticketData = new
            {
                TicketId = ticket.Id,
                EventName = ticket.Event.Name,
                Seat = ticket.SeatNumber,
                Date = ticket.Event.EventStartingDate,
                Owner = ticket.User?.FullName ?? "Khách",
                Issuer = "Hệ thống tự động",
                ConfirmedAt = DateTime.Now
            };

            // --- Thêm block vào blockchain Ganache ---
            string blockDataJson = JsonConvert.SerializeObject(ticketData);
            string txHash = await _blockchain.AddBlockAsync(blockDataJson);

            // Lấy block index cuối cùng (ganache không có trả về trực tiếp hash struct, bạn có thể lưu hash = txHash tạm)
            ticket.QRHash = txHash;
            ticket.PreviousHash = ""; // Ganache smart contract không lưu previous hash trong C#, có thể để trống hoặc lưu block index nếu muốn

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            return true;
        }

        // ------------------------ Action auto xử lý Payment SUCCESS ------------------------
        [HttpPost]
        [Route("Admin/SuperAdmin/ConfirmTicketsOnPaymentSuccess")]
        public async Task<IActionResult> ConfirmTicketsOnPaymentSuccess(string orderCode)
        {
            if (!long.TryParse(orderCode, out long orderCodeLong))
                return BadRequest(new { success = false, message = "Mã đơn hàng không hợp lệ." });

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderCode == orderCodeLong && p.Status == "SUCCESS");

            if (payment == null)
                return BadRequest(new { success = false, message = $"Không tìm thấy giao dịch thành công {orderCode}." });

            var seats = payment.SelectedSeats.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var types = payment.TicketType.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (seats.Length != types.Length)
                return BadRequest(new { success = false, message = "Dữ liệu ghế không khớp số lượng loại vé." });

            var existingTickets = await _context.Tickets
                .Where(t => t.EventId == payment.EventId &&
                            t.UserId == payment.UserId &&
                            seats.Contains(t.SeatNumber))
                .Select(t => t.SeatNumber)
                .ToListAsync();

            var newTickets = new List<Ticket>();

            for (int i = 0; i < seats.Length; i++)
            {
                if (existingTickets.Contains(seats[i]))
                    continue;

                var ticket = new Ticket
                {
                    EventId = payment.EventId,
                    UserId = payment.UserId,
                    SeatNumber = seats[i],
                    TicketType = types[i],
                    Price = payment.TotalAmount / seats.Length,
                    Status = "Chờ xác nhận",
                    BookedAt = DateTime.Now
                };

                _context.Tickets.Add(ticket);
                newTickets.Add(ticket);
            }

            await _context.SaveChangesAsync();

            foreach (var ticket in newTickets)
                await ProcessTicketConfirmationAndBlockchain(ticket);

            var pending = await _context.PendingPaymentSeats
                .Where(s => s.OrderCode == payment.OrderCode)
                .ToListAsync();

            _context.PendingPaymentSeats.RemoveRange(pending);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Đã tạo + xác nhận {newTickets.Count} vé. Mỗi ghế có 1 QR riêng.",
                totalTickets = newTickets.Count
            });
        }

        // Các action khác giữ nguyên
        public async Task<IActionResult> Index()
        {
            var revenues = _context.Events
               .Include(e => e.Tickets)
               .Select(e => new EventRevenue
               {
                   Event = e,
                   TotalRevenue = e.Tickets
                       .Where(t => t.Status == "Đã xác nhận")
                       .Sum(t => t.Price)
               }).ToList();

            return View(revenues);
        }

        [HttpGet]
        public async Task<IActionResult> Ticket()
        {
            var tickets = await _context.Tickets
               .Include(t => t.Event)
               .Include(t => t.User)
               .Include(t => t.PreviousOwner)
               .Include(t => t.NewOwner)
               .OrderByDescending(t => t.BookedAt)
               .ToListAsync();

            return View(tickets);
        }

        [HttpGet]
        [Route("Admin/SuperAdmin/ViewTicket/{ticketId}")]
        public async Task<IActionResult> ViewTicket(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .ThenInclude(e => e.EventType)
                .Include(t => t.User)
                .Include(t => t.PreviousOwner)
                .Include(t => t.NewOwner)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
                return NotFound("Không tìm thấy vé.");

            ViewBag.IsVerified = false;
            ViewBag.Error = "Chưa xác nhận vé";
            ViewBag.Hash = ticket.QRHash;
            ViewBag.TicketCode = ticket.Id;

            if (!string.IsNullOrEmpty(ticket.QRHash))
            {
                var qrGenerator = new QRCodeGenerator();
                var qrData = qrGenerator.CreateQrCode(ticket.QRHash, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new Base64QRCode(qrData);
                ViewBag.QRCodeImage = qrCode.GetGraphic(20);
            }

            return View("ViewTicket", ticket);
        }
    }
}
