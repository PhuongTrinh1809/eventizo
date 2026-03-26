// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Eventizo.Models;
using Microsoft.AspNetCore.Authentication;

namespace Eventizo.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            var user = await _signInManager.UserManager.GetUserAsync(User);
            if (user != null)
            {
                user.SessionToken = null;
                var updateResult = await _signInManager.UserManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning("Không thể cập nhật SessionToken của user khi logout.");
                }
            }

            // Xóa cookie đăng nhập user
            Response.Cookies.Delete("SessionToken_User");

            // Xóa cookie đăng nhập ẩn danh (nếu có)
            Response.Cookies.Delete("SessionToken_Guest");

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await _signInManager.SignOutAsync();

            _logger.LogInformation("User logged out.");

            return !string.IsNullOrEmpty(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToPage("/Index");
        }

    }
}
