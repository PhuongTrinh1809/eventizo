using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Eventizo.Models;
using Microsoft.AspNetCore.Authentication;

namespace Eventizo.Middlewares
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // ✅ BỎ QUA hoàn toàn các route KHÔNG được phép logout
            if (
                path.StartsWith("/identity") ||        // Login / Register
                path.StartsWith("/api/") ||             // Webhook, API
                path.Contains("/static/") ||            // Static files
                path.StartsWith("/payment") ||          // 🔥 PayOS return
                path.StartsWith("/payos")               // nếu có route riêng
            )
            {
                await _next(context);
                return;
            }

            // Resolve UserManager
            using var scope = context.RequestServices.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(context.User);

                if (user != null)
                {
                    var cookieToken = context.Request.Cookies["SessionToken_User"];
                    var dbToken = user.SessionToken;

                    bool tokenInvalid =
                        string.IsNullOrEmpty(cookieToken) ||
                        string.IsNullOrEmpty(dbToken) ||
                        cookieToken != dbToken;

                    // 🔒 CHỈ logout khi truy cập TRANG CẦN BẢO MẬT
                    bool isProtectedPage =
                        path.StartsWith("/account") ||
                        path.StartsWith("/order") ||
                        path.StartsWith("/ticket") ||
                        path.StartsWith("/cart") ||
                        path.StartsWith("/checkout");

                    if (tokenInvalid && isProtectedPage)
                    {
                        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                        context.Response.Cookies.Delete("SessionToken_User");

                        context.Response.Redirect("/Identity/Account/Login?expired=true");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
