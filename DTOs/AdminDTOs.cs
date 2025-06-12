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

}