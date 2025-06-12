// Models/PasswordResetToken.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyNguoiDungApi.Models
{
    public class PasswordResetToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } 

        [Required]
        [MaxLength(255)]
        public string Token { get; set; } = string.Empty; 

        public DateTime CreatedAt { get; set; } 
        public DateTime ExpiresAt { get; set; } 

        public bool IsUsed { get; set; } = false; 

        // Navigation property
        [ForeignKey("UserId")]
        public User User { get; set; } = null!; // Người dùng liên quan
    }
}