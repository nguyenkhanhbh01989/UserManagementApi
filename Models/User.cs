
using System.ComponentModel.DataAnnotations; 

namespace QuanLyNguoiDungApi.Models
{
    // Lớp User đại diện cho một người dùng trong hệ thống == một bảng trong db
    public class User
    {
        // Thuộc tính Id là khóa chính tự động tăng 
        [Key] // Đánh dấu đây là khóa chính
        public int Id { get; set; }

        // Tên đăng nhập 
        [Required] // Bắt buộc phải có giá trị
        [MaxLength(50)] // Giới hạn độ dài tối đa 
        public string Username { get; set; } = string.Empty; // Gán giá trị mặc định để tránh lỗi null

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Địa chỉ email của người dùng, có thể không bắt buộc
        [MaxLength(100)]
        public string? Email { get; set; } // Dùng ? để cho phép giá trị null

        // Thời gian người dùng được tạo
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Ghi lại thời gian tạo theo giờ UTC
    }
}