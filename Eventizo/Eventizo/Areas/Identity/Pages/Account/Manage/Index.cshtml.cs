// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Eventizo.Models; // Namespace chứa ApplicationUser
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eventizo.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Phone]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Họ và tên")]
            public string FullName { get; set; }

            [DataType(DataType.Date)]
            [Display(Name = "Ngày sinh")]
            public DateTime? DOB { get; set; }

            [Display(Name = "Giới tính")]
            public string Gender { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                // Lấy dữ liệu từ User hiện tại đổ vào Form
                FullName = user.FullName,
                DOB = user.DOB,
                Gender = user.Gender
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }
            // --- 1. Xử lý số điện thoại (Logic chuẩn Identity) ---
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Lỗi: Không thể cập nhật số điện thoại.";
                    return RedirectToPage();
                }
            }

            // --- 2. Xử lý cập nhật thông tin cá nhân (FullName, DOB, Gender) ---
            bool hasChanges = false;

            // Kiểm tra FullName có thay đổi không
            if (Input.FullName != user.FullName)
            {
                user.FullName = Input.FullName;
                hasChanges = true;
            }

            // Kiểm tra DOB có thay đổi không
            if (Input.DOB != user.DOB)
            {
                user.DOB = Input.DOB;
                hasChanges = true;
            }

            // Kiểm tra Gender có thay đổi không
            if (Input.Gender != user.Gender)
            {
                user.Gender = Input.Gender;
                hasChanges = true;
            }

            // Nếu có thay đổi ở mục 2 thì mới gọi lệnh Update Database
            if (hasChanges)
            {
                user.LastUpdated = DateTime.Now; // Cập nhật thời gian sửa đổi (tùy chọn)

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Lỗi: Đã xảy ra sự cố khi cập nhật hồ sơ.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật thành công!";
            return RedirectToPage();
        }
    }
}