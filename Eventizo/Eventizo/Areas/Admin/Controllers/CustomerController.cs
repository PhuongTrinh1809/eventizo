using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Eventizo.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Eventizo.Data;
using Eventizo.ViewModel;
using Eventizo.Services;

[Area("Admin")]
[Route("Admin/[controller]/[action]")]
public class CustomerController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly PointService _pointService;

    public CustomerController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _pointService = new PointService(context); // ✅ Sử dụng PointService
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var userVMs = new List<Eventizo.ViewModel.UserWithRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // Lấy tổng điểm qua PointService
            var totalPoints = _pointService.GetTotalPoints(user.Id);

            // Cập nhật MemberLevel nếu cần
            _pointService.UpdateLevel(user);

            userVMs.Add(new Eventizo.ViewModel.UserWithRoleViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? "Customer",
                CreatedDate = user.CreatedDate,
                IsActive = user.IsActive,
                Points = totalPoints,
                MemberLevel = user.MemberLevel // <-- thêm MemberLevel
            });
        }

        var sorted = userVMs.OrderByDescending(u => u.CreatedDate).ToList();

        var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        ViewBag.AllRoles = allRoles.Select(r => new SelectListItem
        {
            Value = r,
            Text = r
        }).ToList();

        return View(sorted);
    }

    [HttpGet]
    public async Task<IActionResult> EditRole(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

        ViewBag.AllRoles = allRoles.Select(r => new SelectListItem
        {
            Value = r,
            Text = r
        }).ToList();

        var vm = new EditUserRoleViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            CurrentRole = currentRoles.FirstOrDefault() ?? "",
            Roles = allRoles
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> EditRole(string userId, string selectedRole)
    {
        if (string.IsNullOrWhiteSpace(selectedRole))
        {
            TempData["Error"] = "Bạn phải chọn một quyền hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        var existingRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, existingRoles);
        await _userManager.AddToRoleAsync(user, selectedRole);

        TempData["Success"] = "Cập nhật quyền thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DisableUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Đã vô hiệu hóa tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> EnableUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Tài khoản đã được kích hoạt lại.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "UserId không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var tickets = await _context.Tickets
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (tickets.Any())
            {
                _context.Tickets.RemoveRange(tickets);
                await _context.SaveChangesAsync();
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = "Xoá tài khoản thành công.";
            }
            else
            {
                TempData["Error"] = "Xoá tài khoản thất bại: " +
                                    string.Join(", ", result.Errors.Select(e => e.Description));
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Lỗi khi xoá: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}