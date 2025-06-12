// Models/UserRole.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyNguoiDungApi.Models
{
    public class UserRole
    {
        // Khóa ngoại to tb User
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!; 

        // Khóa ngoại to tb Role
        [ForeignKey("Role")]
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!; 
    }
}