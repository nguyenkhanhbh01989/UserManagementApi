// DTOs/UserDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyNguoiDungApi.DTOs
{
    // DTO cho việc xem thông tin chi tiết của người dùng
    public class UserDetailsDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // DTO cho việc xem danh sách người dùng (chỉ các thông tin cần thiết)
    public class UserDetailsForListDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    // DTO cho việc cập nhật thông tin người dùng (Username hoặc Email)
    public class UserUpdateDto
    {
        [StringLength(255, MinimumLength = 3, ErrorMessage = "Tên người dùng phải có từ 3 đến 255 ký tự.")]
        public string? Username { get; set; }

        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự.")]
        public string? Email { get; set; }
    }

    // DTO cho việc thay đổi mật khẩu
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mật khẩu cũ là bắt buộc.")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}