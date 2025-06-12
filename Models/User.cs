// Models/User.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace QuanLyNguoiDungApi.Models
{
    public class User
    {
        [Key] // Đánh dấu Id là khóa chính
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Id tự động tăng
        public int Id { get; set; }

        [Required] // Bắt buộc phải có
        [MaxLength(255)] 
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
        //ĐỊNH NGHĨA MỐI QUAN HỆ VỚI USER ROLE
        public ICollection<UserRole>? UserRoles { get; set; }
    }
}