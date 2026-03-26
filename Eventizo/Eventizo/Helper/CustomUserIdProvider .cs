using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Eventizo.Helper
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Trả về ID người dùng (ClaimTypes.NameIdentifier)
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
