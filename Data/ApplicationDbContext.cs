
using Microsoft.EntityFrameworkCore; 
using QuanLyNguoiDungApi.Models; 

namespace QuanLyNguoiDungApi.Data
{
    // ApplicationDbContext sẽ kế thừa từ DbContext của Entity Framework Core
    public class ApplicationDbContext : DbContext
    {
        // Constructor để nhận các tùy chọn cấu hình từ bên ngoài 
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSet<User> đại diện cho một tập hợp các đối tượng User trong cơ sở dữ liệu.
        // Khi EF Core đọc lớp này, nó sẽ tạo ra một bảng có tên là "Users" (theo quy ước)
        // trong cơ sở dữ liệu, với các cột tương ứng với các thuộc tính của lớp User.
        public DbSet<User> Users { get; set; }

        //có thể ghi đè phương thức OnModelCreating để cấu hình nâng cao cho các model
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ví dụ: Đảm bảo Username là duy nhất
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Gọi phương thức của lớp cha để đảm bảo các cấu hình mặc định khác vẫn được áp dụng
            base.OnModelCreating(modelBuilder);
        }
    }
}