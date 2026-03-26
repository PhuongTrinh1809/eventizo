using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Eventizo.Models;
using Eventizo.Data;

namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class TicketManageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketManageController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();  
        }

        [HttpDelete]
        public async Task<IActionResult> ClearAllSeats(int eventId)
        {
            try
            {
                var ticketsToDelete = _context.Tickets.Where(t => t.EventId == eventId).ToList();
                _context.Tickets.RemoveRange(ticketsToDelete);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi xóa ghế: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult GetOccupiedSeats(int eventId)
        {
            var seats = _context.Tickets
                .Where(t => t.EventId == eventId)
                .Select(t => t.SeatNumber)
                .ToList();
            return Json(seats);
        }

        // Thêm action này để cập nhật danh sách ghế đã đặt
        [HttpPost]
        public async Task<IActionResult> UpdateOccupiedSeats([FromBody] SeatUpdateModel model)
        {
            if (model == null || model.EventId <= 0 || model.OccupiedSeats == null)
                return BadRequest("Dữ liệu không hợp lệ");

            try
            {
                // Lấy danh sách vé hiện có của event
                var existingTickets = _context.Tickets.Where(t => t.EventId == model.EventId).ToList();

                // Ghế mới được đặt (danh sách cập nhật)
                var updatedSeats = model.OccupiedSeats;

                // Ghế cũ không còn trong danh sách mới => xóa vé tương ứng (bỏ đặt)
                var ticketsToRemove = existingTickets
                    .Where(t => !updatedSeats.Contains(t.SeatNumber))
                    .ToList();
                _context.Tickets.RemoveRange(ticketsToRemove);

                // Ghế mới chưa có trong vé hiện tại => tạo vé mới
                var existingSeatNumbers = existingTickets.Select(t => t.SeatNumber).ToHashSet();
                var seatsToAdd = updatedSeats.Where(s => !existingSeatNumbers.Contains(s)).ToList();

                foreach (var seat in seatsToAdd)
                {
                    var ticket = new Ticket
                    {
                        EventId = model.EventId,
                        SeatNumber = seat,
                        Price = 0, // hoặc lấy giá từ chỗ khác
                        Status = "Chờ xác nhận"
                        // Bạn có thể gán thêm các thuộc tính cần thiết khác
                    };
                    _context.Tickets.Add(ticket);
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật ghế thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi cập nhật ghế: {ex.Message}");
            }
        }
    }

    public class SeatUpdateModel
    {
        public int EventId { get; set; }
        public List<string> OccupiedSeats { get; set; }
    }
}
