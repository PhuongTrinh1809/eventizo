using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eventizo.Areas.EventAdmin.Controllers
{
    [Area("EventAdmin")]
    public class SuperEventController : Controller
    {
        private readonly IEventRepository _eventRepository;
        private readonly IEventTypeRepository _eventTypeRepository;
        public SuperEventController(IEventRepository eventRepository, IEventTypeRepository eventTypeRepository)
        {
            _eventRepository = eventRepository;
            _eventTypeRepository = eventTypeRepository;
        }

        // Hiển thị danh sách sản phẩm 
        public async Task<IActionResult> Index()
        {
            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị danh sách
            var events = await _eventRepository.GetAllAsync();
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

        // Hiển thị form thêm sản phẩm mới 
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");

            return View();
        }

        // Xử lý thêm sản phẩm mới 
        [HttpPost]
        public async Task<IActionResult> Add(Event even, IFormFile imageUrl)
        {
            if (ModelState.IsValid)
            {
                if (imageUrl != null)
                {
                    // Lưu hình ảnh đại diện tham khảo bài 02 hàm SaveImage 
                    even.ImageUrl = await SaveImage(imageUrl);
                }

                await _eventRepository.AddAsync(even);
                return RedirectToAction(nameof(Index));
            }
            // Nếu ModelState không hợp lệ, hiển thị form với dữ liệu đã nhập 
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");
            return View(even);
        }

        [HttpPost]
        [ActionName("AddWithImages")]// đổi tên khi dò URL của Add thành AddWithImages
        public async Task<IActionResult> Add(Event even, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            if (ModelState.IsValid)
            {
                // Lưu ảnh đại diện
                if (imageUrl != null)
                {
                    even.ImageUrl = await SaveImage(imageUrl);
                }

                // Lưu nhiều ảnh khác
                if (imageUrls != null && imageUrls.Count > 0)
                {
                    var imagePaths = new List<string>();
                    foreach (var file in imageUrls)
                    {
                        imagePaths.Add(await SaveImage(file));
                    }
                }

                await _eventRepository.AddAsync(even);
                return RedirectToAction("Index");
            }

            return View(even);
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

        // Hiển thị form cập nhật sản phẩm 
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

        // Xử lý cập nhật sản phẩm 
        [HttpPost]
        public async Task<IActionResult> Update(int id, Event even, IFormFile imageUrl)
        {
            ModelState.Remove("ImageUrl"); // Loại bỏ xác thực ModelState cho ImageUrl
            if (id != even.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {

                var existingEvent = await _eventRepository.GetByIdAsync(id); // Giả định có phương thức GetByIdAsync 

                if (imageUrl == null)
                {
                    even.ImageUrl = existingEvent.ImageUrl;
                }
                else
                {
                    // Lưu hình ảnh mới 
                    even.ImageUrl = await SaveImage(imageUrl);
                }

                // Cập nhật các thông tin khác của sản phẩm 
                existingEvent.Name = even.Name;
                existingEvent.Description = even.Description;
                existingEvent.EventStartingDate = even.EventStartingDate;
                existingEvent.EventEndingDate = even.EventEndingDate;
                existingEvent.Place = even.Place;
                existingEvent.Status = even.Status;
                existingEvent.Capacity = even.Capacity;
                existingEvent.EventTypeId = even.EventTypeId;
                existingEvent.ImageUrl = even.ImageUrl;

                await _eventRepository.UpdateAsync(existingEvent);

                return RedirectToAction(nameof(Index));
            }
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            ViewBag.EventTypes = new SelectList(eventTypes, "Id", "Name");
            return View(even);
        }

        // Hiển thị form xác nhận xóa sản phẩm 
        public async Task<IActionResult> Delete(int id)
        {
            var even = await _eventRepository.GetByIdAsync(id);
            if (even == null)
            {
                return NotFound();
            }
            return View(even);
        }
        // Xử lý xóa sản phẩm 
        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _eventRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> ThePastAsync()
        {
            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị danh sách
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }
        public async Task<IActionResult> ThePresentAsync()
        {
            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị danh sách
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }
        public async Task<IActionResult> TheFutureAsync()
        {
            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị danh sách
            var events = await _eventRepository.GetAllAsync();
            return View(events);
        }
        public async Task<IActionResult> Display(int id)
        {
            var even = await _eventRepository.GetByIdAsync(id);
            if (even == null)
            {
                return NotFound();
            }
            return View(even);
        }
    }
}
