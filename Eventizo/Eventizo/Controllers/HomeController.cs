using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Globalization;
using System.Text;
using QRCoder;
using Eventizo.Data;
using Eventizo.ViewModel;

namespace Eventizo.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEventRepository _eventRepository;
        private readonly ILogger<HomeController> _logger;
        private readonly IEventTypeRepository _eventTypeRepository;
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            IEventRepository eventRepository,
            IEventTypeRepository eventTypeRepository,
            IProductRepository productRepository,
            ApplicationDbContext context)
        {
            _logger = logger;
            _eventRepository = eventRepository;
            _eventTypeRepository = eventTypeRepository;
            _productRepository = productRepository;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy tất cả sự kiện có liên quan
            var allEvents = await _context.Events
                .Include(e => e.EventType)
                .Include(e => e.Products)
                    .ThenInclude(p => p.Payments)
                .ToListAsync();

            // Chuyển thành EventRevenue
            var eventRevenues = allEvents.Select(e => new EventRevenue
            {
                Event = e,
                TotalRevenue = _context.Payments
                        .Where(p => p.EventId == e.Id && p.Status == "SUCCESS") // chỉ tính payment thành công
                        .Sum(p => p.TotalAmount)
            }).ToList();

            var topRevenueEvents = eventRevenues
                .Where(e => e.Event.EventStartingDate < DateTime.Now)
                .OrderByDescending(e => e.TotalRevenue)
                .Take(5)
                .ToList();

            var upcomingEvents = eventRevenues
                .Where(e => e.Event.Status == "Sắp diễn ra")
                .OrderBy(e => e.Event.EventStartingDate)
                .Take(5)
                .ToList();

            var currentMonthEvents = eventRevenues
                .Where(e => e.Event.EventStartingDate.Month == DateTime.Now.Month
                         && e.Event.EventStartingDate.Year == DateTime.Now.Year)
                .OrderBy(e => e.Event.EventStartingDate)
                .ToList();

            var bestSellingEvents = eventRevenues
                .OrderByDescending(e => e.TotalRevenue)
                .ToList();

            var model = new HomePageViewModel
            {
                UpcomingEvents = upcomingEvents,
                TopRevenueEvents = topRevenueEvents,
                CurrentMonthEvents = currentMonthEvents,
                BestSellingEvents = bestSellingEvents,
            };

            return View(model);
        }

        public IActionResult Display(int id)
        {
            var ev = _context.Events
                .Include(e => e.EventType)
                .FirstOrDefault(e => e.Id == id);

            if (ev == null)
            {
                return NotFound();
            }
            return View(ev);
        }

        [Authorize]
        public IActionResult DetailsTour(int id)
        {
            var eventItem = _context.Events.Include(e => e.EventType).FirstOrDefault(e => e.Id == id && e.EventType.Name == "Tour");
            if (eventItem == null)
            {
                return NotFound();
            }

            return View(eventItem);
        }

        [Authorize]
        public IActionResult DetailsConcert(int id)
        {
            var eventItem = _context.Events.Include(e => e.EventType).FirstOrDefault(e => e.Id == id && e.EventType.Name == "Concert");
            if (eventItem == null)
            {
                return NotFound();
            }

            return View(eventItem);
        }

        [Authorize]
        public IActionResult DetailsLiveshow(int id)
        {
            var eventItem = _context.Events.Include(e => e.EventType).FirstOrDefault(e => e.Id == id && e.EventType.Name == "Liveshow");
            if (eventItem == null)
            {
                return NotFound();
            }

            return View(eventItem);
        }

        public IActionResult ProductDisplay(int id)
        {
            var product = _context.Products
                .Include(e => e.Category)
                .FirstOrDefault(e => e.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        [Authorize]
        public async Task<IActionResult> Product()
        {
            var allProducts = await _context.Products.ToListAsync();
            return View(allProducts);
        }

        [Authorize]
        public async Task<IActionResult> Tour()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        [Authorize]
        public async Task<IActionResult> Concert()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        [Authorize]
        public async Task<IActionResult> LiveShow()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        public async Task<IActionResult> Profile()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        // Trang hiển thị toàn bộ sản phẩm
        public async Task<IActionResult> AllEvents()
        {
            var AllEvents = await _eventRepository.GetAllAsync();
            return View(AllEvents);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> BuyTicket(int id)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        public async Task<IActionResult> Stage2(int id)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        public async Task<IActionResult> MyTickets()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;

            // 🔹 1. Tự động huỷ các chuyển nhượng đã quá 1 ngày
            var expiredTransfers = await _context.Tickets
                .Where(t =>
                    t.TransferStatus == "Pending" &&
                    t.TransferRequestedAt != null &&
                    t.TransferRequestedAt.Value.AddDays(1) <= now
                )
                .ToListAsync();

            if (expiredTransfers.Any())
            {
                foreach (var ticket in expiredTransfers)
                {
                    ticket.TransferStatus = "Cancelled";
                    ticket.NewOwnerId = null;
                    ticket.IsTransferAccepted = false;
                }

                await _context.SaveChangesAsync();
            }

            // 🔹 2. Vé người dùng đang sở hữu
            var tickets = await _context.Tickets
                .Where(t => t.UserId == userId)
                .Include(t => t.Event)
                .OrderByDescending(t => t.BookedAt)
                .ToListAsync();

            // 🔹 3. Vé đang chờ chuyển nhượng (CHƯA HẾT HẠN)
            var incomingTransfers = await _context.Tickets
                .Include(t => t.Event)
                .Include(t => t.PreviousOwner)
                .Where(t =>
                    t.NewOwnerId == userId &&
                    t.TransferStatus == "Pending" &&
                    t.TransferRequestedAt != null &&
                    t.TransferRequestedAt.Value.AddDays(1) > now
                )
                .ToListAsync();

            // 🔹 4. ViewModel
            var model = new MyTicketsViewModel
            {
                MyTickets = tickets,
                IncomingTransfers = incomingTransfers
            };

            return View(model);
        }

        public IActionResult EventSwiper()
        {
            var events = _context.Events.ToList();
            return View(events);
        }

        [Authorize]
        public async Task<IActionResult> All()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        [Authorize]
        public async Task<IActionResult> InMonth()
        {
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }

        public IActionResult Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return View("SearchResult", new List<Event>());
            }

            string normalizedTerm = RemoveDiacritics(searchTerm.ToLower());

            var events = _context.Events
                .Include(e => e.EventType)
                .AsEnumerable()
                .Where(e =>
                    RemoveDiacritics(e.Name.ToLower()).Contains(normalizedTerm) ||
                    RemoveDiacritics((e.Description ?? "").ToLower()).Contains(normalizedTerm) ||
                    RemoveDiacritics(e.Place.ToLower()).Contains(normalizedTerm) ||
                    RemoveDiacritics((e.EventType != null ? e.EventType.Name : "").ToLower()).Contains(normalizedTerm)
                )
                .ToList();

            return View("SearchResult", events);
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }

        [Authorize]
        public IActionResult ShowQRCode(int id)
        {
            var ticket = _context.Tickets
                .Include(t => t.Event)
                .ThenInclude(e => e.EventType)
                .FirstOrDefault(t => t.Id == id && t.Status == "Đã xác nhận");

            if (ticket == null)
                return NotFound();

            // 🔹 Nếu vé có QRHash thì chỉ lấy lại QR code cũ
            if (!string.IsNullOrEmpty(ticket.QRHash))
            {
                var qrBase64 = GenerateQRBase64(ticket.QRHash);
                ViewBag.QRCodeImage = qrBase64;
                ViewBag.Hash = ticket.QRHash;
                ViewBag.Previous = ticket.PreviousHash;
                ViewBag.TicketCode = $"{ticket.Event.EventType.Name}-{ticket.Event.Id}-{ticket.SeatNumber}";
                ViewBag.IsVerified = true;
                return View(ticket);
            }
            // ⚠️ Nếu chưa được xác nhận, báo lỗi
            ViewBag.Error = "Vé chưa được xác nhận, không thể sinh QR.";
            ViewBag.IsVerified = false;
            return View("BlockchainError");
        }

        private string GenerateQRBase64(string hash)
        {
            var qrGen = new QRCodeGenerator();
            var qrData = qrGen.CreateQrCode(hash, QRCodeGenerator.ECCLevel.Q);
            var qr = new PngByteQRCode(qrData);
            return "data:image/png;base64," + Convert.ToBase64String(qr.GetGraphic(20));
        }
    }
}