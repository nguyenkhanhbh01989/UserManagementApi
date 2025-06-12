// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using QuanLyNguoiDungApi.Models; 

namespace QuanLyNguoiDungApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;

        // Phương thức này được gọi khi mô hình đang được tạo
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình cho bảng User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique(); // Username ko trùng 
            });

            // CẤU HÌNH MỐI QUAN HỆ NHIỀU-NHIỀU GIỮA USER VÀ ROLE QUA USERROLE
            modelBuilder.Entity<UserRole>(entity =>
            {
                // Định nghĩa khóa chính tổ hợp
                entity.HasKey(ur => new { ur.UserId, ur.RoleId });

                // Cấu hình mối quan hệ User - UserRole
                entity.HasOne(ur => ur.User)
                      .WithMany(u => u.UserRoles)
                      .HasForeignKey(ur => ur.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // Khi User bị xóa, các UserRole liên quan cũng bị xóa

                // Cấu hình mối quan hệ Role <-> UserRole
                entity.HasOne(ur => ur.Role)
                      .WithMany(r => r.UserRoles)
                      .HasForeignKey(ur => ur.RoleId)
                      .OnDelete(DeleteBehavior.Cascade); // Khi Role bị xóa, các UserRole liên quan cũng bị xóa
            });


            //them dữ liệu sẵn 
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "User" }
            );
        }
    }
}