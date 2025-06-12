using Microsoft.EntityFrameworkCore;
using QuanLyNguoiDungApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuanLyNguoiDungApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Đăng ký dịch vụ cho các Controller của API
builder.Services.AddControllers();

// Cấu hình Swagger/OpenAPI để tạo tài liệu API tự động và giao diện kiểm thử
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// bdau CẤU HÌNH :

// 1. Cấu hình cho Entity Framework Core và SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình JWT Authentication
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
builder.Services.AddAuthentication()
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/api/Auth/login";
    options.AccessDeniedPath = "/api/Auth/accessdenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// 4. Thêm Authorization service
builder.Services.AddAuthorization();

// 5. Cấu hình CORS 
builder.Services.AddCors(options =>
{
    
    options.AddPolicy("AllowSpecificOrigins",
        builder => builder.WithOrigins("http://localhost:80", "http://localhost:443" , "http://localhost:5173") //các cổng fontend
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials()); // Rất quan trọng cho Cookie/Session authentication với CORS
});

//ĐĂNG KÝ EMAIL SERVICE 
builder.Services.AddTransient<IEmailService, EmailService>(); // Hoặc AddScoped tùy

// kt CẤU HÌNH 

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

// Kích hoạt Middleware CORS 
// Đảm bảo UseCors() đặt trước UseAuthentication() và UseAuthorization()
app.UseCors("AllowSpecificOrigins"); 


// Kích hoạt Middleware xác thực (Authentication) và ủy quyền (Authorization)
app.UseAuthentication();
app.UseAuthorization();

// Ánh xạ các Controller của bạn tới các endpoint HTTP
app.MapControllers();

// Khởi chạy ứng dụng Web API
app.Run();