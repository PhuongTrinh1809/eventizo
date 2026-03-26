using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Eventizo.Data;

namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    //[Route("Admin/[controller]/[action]")] //định nghĩa đường dẫn URL
    public class EventController : Controller
    {
        private readonly IEventRepository _eventRepository;
        private readonly IEventTypeRepository _eventTypeRepository;
        private readonly ApplicationDbContext _context;
        public EventController(IEventRepository eventRepository, IEventTypeRepository eventTypeRepository, ApplicationDbContext context)
        {
            _eventRepository = eventRepository;
            _eventTypeRepository = eventTypeRepository;
            _context = context;
        }

        // Hiển thị danh sách sự kiện 
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 6; // số lượng sự kiện trên mỗi trang

            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị

            var allEvents = await _eventRepository.GetAllAsync();
            var totalItems = allEvents.Count();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var events = allEvents
                .OrderBy(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(events);
        }

        private async Task UpdateStatusAsync()
        {
            var events = await _eventRepository.GetAllAsync();
            var now = DateTime.Now;

            foreach (var ev in events)
            {
                var startDateTime = ev.EventStartingDate;
                var endDateTime = ev.EventEndingDate;

                string newStatus;
                if (now < startDateTime)
                    newStatus = "Sắp diễn ra";
                else if (now >= startDateTime && now <= endDateTime)
                    newStatus = "Đang diễn ra";
                else
                    newStatus = "Đã diễn ra";

                if (ev.Status != newStatus)
                {
                    ev.Status = newStatus;
                    await _eventRepository.UpdateStatusAsync(ev.Id, newStatus); // đảm bảo bạn đã có hàm này trong EFEventRepository
                }
            }
        }

        // Hiển thị form thêm sự kiện mới 
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");

            return View(new Event
            {
                EventStartingDate = DateTime.Now,
                EventEndingDate = DateTime.Now.AddHours(2)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Add(Event even, IFormFile imageUrl, List<IFormFile> images, List<string> ticketlist)
        {
            if (ModelState.IsValid)
            {
                // Xử lý loại vé (TicketType)
                even.TicketType = ticketlist != null && ticketlist.Count > 0
                                  ? string.Join(", ", ticketlist.Where(f => !string.IsNullOrWhiteSpace(f)))
                                  : null;


                // Xử lý giá giảm (PriceReduced)
                if (even.PriceReducedMin == null || even.PriceReducedMin <= 0 || even.PriceReducedMin >= even.PriceMin)
                {
                    even.PriceReducedMin = null; // Không hiển thị giá giảm nếu không hợp lệ
                }
                if (even.PriceReducedMax == null || even.PriceReducedMax <= 0 || even.PriceReducedMax >= even.PriceMin)
                {
                    even.PriceReducedMax = null; // Không hiển thị giá giảm nếu không hợp lệ
                }

                // Xử lý ảnh chính (Event Image)
                if (imageUrl != null)
                {
                    even.ImageUrl = await SaveImage(imageUrl);
                }

                // Xử lý danh sách ảnh bổ sung (Additional Images)
                if (images != null && images.Count > 0)
                {
                    even.Images = new List<EventImage>();
                    foreach (var image in images)
                    {
                        var imageUrlPath = await SaveImage(image);
                        even.Images.Add(new EventImage { Url = imageUrlPath });
                    }
                }

                await _eventRepository.AddAsync(even);
                return RedirectToAction("Index");
            }
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");
            return View("Add", even);
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            //Thay đổi đường dẫn theo cấu hình của bạn 
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName; // Trả về đường dẫn tương đối 
        }

        // Hiển thị thông tin chi tiết sự kiện
        public async Task<IActionResult> Display(int id)
        {
            var even = await _eventRepository.GetByIdAsync(id);
            if (even == null)
            {
                return NotFound();
            }
            return View(even);
        }

        // Hiển thị form cập nhật sự kiện 
        public async Task<IActionResult> Update(int id)
        {
            var even = await _eventRepository.GetByIdAsync(id);
            if (even == null)
            {
                return NotFound();
            }

            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name", even.EventTypeId);
            return View(even);
        }

        // Xử lý cập nhật sự kiện 
        [HttpPost]
        public async Task<IActionResult> Update(int id, Event even, IFormFile imageUrl, List<string>? ticketlist)
        {
            // Ghi log để kiểm tra giá trị nhận được từ form
            Console.WriteLine("Received Ticket List:");
            if (ticketlist != null && ticketlist.Any())
            {
                foreach (var flavor in ticketlist)
                {
                    Console.WriteLine($"Flavor: {flavor}");
                }
            }
            else
            {
                Console.WriteLine("Ticket List is EMPTY or NULL");
            }

            ModelState.Remove("ImageUrl"); // Loại bỏ xác thực ModelState cho ImageUrl
            if (id != even.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {

                var existingEvent = await _eventRepository.GetByIdAsync(id);  

                if (imageUrl == null)
                {
                    even.ImageUrl = existingEvent.ImageUrl;
                }
                else
                {
                    // Lưu hình ảnh mới 
                    even.ImageUrl = await SaveImage(imageUrl);
                }

                // Cập nhật các thông tin khác của sự kiện 
                existingEvent.Name = even.Name;
                existingEvent.PriceMin = even.PriceMin;
                existingEvent.PriceMin = even.PriceMin;
                existingEvent.PriceMax = even.PriceMax;
                existingEvent.PriceReducedMax = even.PriceReducedMax;
                existingEvent.Description = even.Description;
                existingEvent.EventStartingDate = even.EventStartingDate;
                existingEvent.EventEndingDate = even.EventEndingDate;
                existingEvent.Place = even.Place;
                existingEvent.Status = even.Status;
                existingEvent.Capacity = even.Capacity;
                existingEvent.EventTypeId = even.EventTypeId;
                existingEvent.ImageUrl = even.ImageUrl;

                // ✅ Cập nhật TicketType (Loại vé)
                existingEvent.TicketType = ticketlist != null && ticketlist.Any()
                    ? string.Join(", ", ticketlist.Where(f => !string.IsNullOrWhiteSpace(f)))
                    : "";

                await _eventRepository.UpdateAsync(existingEvent);
                return RedirectToAction(nameof(Index));
            }
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");
            return View(even);
        }

        // Hiển thị form xác nhận xóa sự kiện 
        public async Task<IActionResult> Delete(int id)
        {
            var even = await _eventRepository.GetByIdAsync(id);
            if (even == null)
            {
                return NotFound();
            }
            return View(even);
        }

        // Xử lý xóa sự kiện 
        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _eventRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
