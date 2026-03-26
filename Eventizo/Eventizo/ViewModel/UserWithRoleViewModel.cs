namespace Eventizo.ViewModel
{
    public class UserWithRoleViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }

        // 👉 Thêm dòng này (không cần bảng DB)
        public int Points { get; set; }
        public string MemberLevel { get; set; }
    }
}