using Eventizo.Data;
using Eventizo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Eventizo.Areas.UsersAdmin.Controllers
{
    [ApiController]
    [Route("api/checkin")]
    public class CheckInApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly BlockchainService _blockchain;
        private readonly ILogger<CheckInApiController> _logger;

        public CheckInApiController(ApplicationDbContext context, BlockchainService blockchain, ILogger<CheckInApiController> logger)
        {
            _context = context;
            _blockchain = blockchain;
            _logger = logger;
        }

        [HttpGet("verify")]
        public async Task<IActionResult> Verify(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                _logger.LogWarning("Check-in failed: hash is empty.");
                return BadRequest(new { valid = false, message = "Hash không được để trống." });
            }

            string normalizedHash = hash.Trim().ToLower();

            try
            {
                // Lấy vé từ DB để có thể update
                var ticket = await _context.Tickets
                    .Include(t => t.User)
                    .Include(t => t.Event)
                    .FirstOrDefaultAsync(t => t.QRHash != null &&
                        t.QRHash.Trim().ToLower() == normalizedHash);

                if (ticket == null)
                {
                    _logger.LogWarning("Check-in failed: QR hash not found in DB. Hash: {Hash}", hash);
                    return NotFound(new { valid = false, message = "Vé không hợp lệ hoặc chưa tồn tại trong hệ thống." });
                }

                // ✅ Kiểm tra vé đã được quét chưa
                if (ticket.IsCheckedIn)
                {
                    _logger.LogWarning("Check-in failed: ticket already used. TicketId: {TicketId}", ticket.Id);
                    return BadRequest(new { valid = false, message = "Vé này đã được quét trước đó." });
                }

                // Kiểm tra transaction tồn tại trên blockchain
                var tx = await _blockchain.GetTransactionByHashAsync(normalizedHash);
                if (tx == null)
                {
                    _logger.LogWarning("Check-in failed: transaction not found on blockchain. Hash: {Hash}", hash);
                    return NotFound(new { valid = false, message = "Vé không tồn tại trên blockchain." });
                }

                // ✅ Đánh dấu vé đã check-in
                ticket.IsCheckedIn = true;
                ticket.CheckedInAt = DateTime.Now;
                _context.Tickets.Update(ticket);
                await _context.SaveChangesAsync();

                // Trả về thông tin vé + blockchain
                return Ok(new
                {
                    valid = true,
                    ticketId = ticket.Id,
                    user = ticket.User?.UserName ?? "Khách",
                    eventName = ticket.Event?.Name ?? "Không xác định",
                    seat = ticket.SeatNumber,
                    checkedInAt = ticket.CheckedInAt,
                    blockchainTxHash = tx.TransactionHash,
                    blockchainFrom = tx.From,
                    blockchainTo = tx.To,
                    blockchainValue = tx.Value.Value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check-in failed: unexpected error. Hash: {Hash}", hash);
                return StatusCode(500, new { valid = false, message = "Lỗi hệ thống, vui lòng thử lại sau." });
            }
        }
    }
}
