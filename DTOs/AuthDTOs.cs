// DTOs/AuthDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyNguoiDungApi.DTOs
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "Tên người dùng là bắt buộc.")]
        [StringLength(255, MinimumLength = 3, ErrorMessage = "Tên người dùng phải có từ 3 đến 255 ký tự.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        public string Password { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự.")]
        public string? Email { get; set; }
    }

    public class UserLoginDto
    {
        [Required(ErrorMessage = "Tên người dùng là bắt buộc.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        public string Password { get; set; } = string.Empty;
    }
}