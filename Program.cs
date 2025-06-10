// Program.cs
using Microsoft.EntityFrameworkCore;
using QuanLyNguoiDungApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies; // Thư viện cho Cookie Authentication

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Đăng ký dịch vụ cho các Controller của API
builder.Services.AddControllers();

// Cấu hình Swagger/OpenAPI để tạo tài liệu API tự động và giao diện kiểm thử
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CẤU HÌNH CỦA CHÚNG TA ---

// 1. Cấu hình cho Entity Framework Core và SQL Server
// Đăng ký ApplicationDbContext vào hệ thống Dependency Injection.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình JWT Authentication
// Đăng ký dịch vụ xác thực và chỉ định sử dụng JWT Bearer scheme.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// 3. Cấu hình Cookie Authentication
// Thêm Cookie Authentication scheme.
builder.Services.AddAuthentication() // Thêm vào builder.Services.AddAuthentication() đã có
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/api/Auth/login";
    options.AccessDeniedPath = "/api/Auth/accessdenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Luôn dùng HTTPS
});


// 4. Thêm Authorization service (Dịch vụ ủy quyền)
builder.Services.AddAuthorization();

// --- KẾT THÚC CẤU HÌNH CỦA CHÚNG TA ---

var app = builder.Build();

// Configure the HTTP request pipeline.

// Chỉ bật Swagger UI trong môi trường phát triển
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Bật HTTPS redirection
app.UseHttpsRedirection();

// Kích hoạt Middleware xác thực (Authentication) và ủy quyền (Authorization)
// LƯU Ý QUAN TRỌNG: UseAuthentication() PHẢI ĐỨNG TRƯỚC UseAuthorization()
app.UseAuthentication();
app.UseAuthorization();

// Ánh xạ các Controller của bạn tới các endpoint HTTP
app.MapControllers();

// Khởi chạy ứng dụng Web API
app.Run();