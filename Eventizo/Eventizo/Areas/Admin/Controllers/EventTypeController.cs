using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class EventTypeController : Controller
    {
        private readonly IEventRepository _eventRepository;
        private readonly IEventTypeRepository _eventTypeRepository;
        public EventTypeController(IEventRepository eventRepository,
        IEventTypeRepository eventTypeRepository)
        {
            _eventRepository = eventRepository;
            _eventTypeRepository = eventTypeRepository;
        }
        public async Task<IActionResult> Index()
        {
            var eventTypes = await _eventTypeRepository.GetAllAsync();
            if (eventTypes == null)
            {
                return NotFound("Event type data not found.");
            }
            return View(eventTypes);
        }
        public async Task<IActionResult> Display(int id)
        {
            var eventType = await _eventTypeRepository.GetByIdAsync(id);
            if (eventType == null)
            {
                return NotFound();
            }
            return View(eventType);
        }
        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(EventType eventType)
        {
            if (ModelState.IsValid)
            {
                await _eventTypeRepository.AddAsync(eventType); // Thêm await
                return RedirectToAction(nameof(Index));
            }
            return View(eventType);
        }

        public async Task<IActionResult> Update(int id)
        {
            var eventType = await _eventTypeRepository.GetByIdAsync(id);
            if (eventType == null)
            {
                return NotFound();
            }
            return View(eventType);
        }
        [HttpPost]
        public async Task<IActionResult> Update(int id, EventType eventType)
        {
            if (id != eventType.Id)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                await _eventTypeRepository.UpdateAsync(eventType);
                return RedirectToAction(nameof(Index));
            }
            return View(eventType);
        }
    }
}
