namespace Eventizo.Models
{
    public class UserWithRoleViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } 
        public DateTime CreatedDate { get; set; }
        public string SelectedRole { get; set; }
        public bool IsActive { get; set; }
    }

    public class EditUserRoleViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string CurrentRole { get; set; }

        public List<string> Roles { get; set; }
        public string SelectedRole { get; set; }
    }


}
