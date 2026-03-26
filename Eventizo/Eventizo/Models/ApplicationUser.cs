using Microsoft.AspNetCore.Identity;

namespace Eventizo.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string? Address { get; set; }
        public string? Age { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? SessionToken { get; set; }

        public string? DisplayName { get; set; }
        public ICollection<ChatMessage>? SentMessages { get; set; }
        public ICollection<ChatMessage>? ReceivedMessages { get; set; }
        public ICollection<Conversation>? Conversations { get; set; }
        public int Points { get; set; } = 0;
        public string MemberLevel { get; set; } = "Thành viên mới";
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public virtual ICollection<UserPointHistory> PointHistories { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? DOB { get; set; } // Ngày sinh
        public string? Gender { get; set; } // Giới tính
    }
}
