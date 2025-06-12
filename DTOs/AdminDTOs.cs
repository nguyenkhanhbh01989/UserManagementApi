// DTOs/AdminDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyNguoiDungApi.DTOs
{
    public class UserRoleUpdateDto
    {
        [Required(ErrorMessage = "ID người dùng là bắt buộc.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Tên vai trò là bắt buộc.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Tên vai trò không hợp lệ.")]
        public string RoleName { get; set; } = string.Empty;
    }

    public class AdminUserUpdateDto
    {
        [Required(ErrorMessage = "ID người dùng là bắt buộc.")]
        public int UserId { get; set; }

        [StringLength(255, MinimumLength = 3, ErrorMessage = "Tên người dùng phải có từ 3 đến 255 ký tự.")]
        public string? Username { get; set; }

        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự.")]
        public string? Email { get; set; }
        public bool? IsActive { get; set; }
    }
    // DTO cho yêu cầu reset mật khẩu
    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;
    }

    // DTO cho xác nhận reset mật khẩu
    public class ResetPasswordConfirmDto
    {
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Token là bắt buộc.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

}