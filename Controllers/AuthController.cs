// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using QuanLyNguoiDungApi.Data;
using QuanLyNguoiDungApi.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication; // Thư viện để sử dụng HttpContext.SignInAsync
using Microsoft.AspNetCore.Authentication.Cookies; // Thư viện để sử dụng CookieAuthenticationDefaults
using System.Collections.Generic; // Để dùng List<Claim>
using System.ComponentModel.DataAnnotations;

namespace QuanLyNguoiDungApi.Controllers
{
    // Đánh dấu đây là một Controller API và định nghĩa route mặc định
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // Biến để truy cập Database Context
        private readonly IConfiguration _configuration; // Biến để truy cập cấu hình từ appsettings.json

        // Constructor để inject ApplicationDbContext và IConfiguration
        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Endpoint để đăng ký người dùng mới.
        /// </summary>
        /// <param name="userDto">Đối tượng chứa thông tin đăng ký (Username, Password, Email).</param>
        /// <returns>HTTP 200 OK nếu đăng ký thành công, hoặc HTTP 400 BadRequest nếu có lỗi.</returns>
        [HttpPost("register")] // Định nghĩa đây là một phương thức POST với route "api/Auth/register"
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            // Kiểm tra xem người dùng đã tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Username == userDto.Username))
            {
                // Trả về lỗi nếu username đã tồn tại
                return BadRequest("Tên người dùng đã tồn tại.");
            }

            // Băm mật khẩu trước khi lưu vào cơ sở dữ liệu để bảo mật
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password);

            // Tạo đối tượng User mới
            var user = new User
            {
                Username = userDto.Username,
                PasswordHash = passwordHash,
                Email = userDto.Email,
                CreatedAt = DateTime.UtcNow // Ghi lại thời gian tạo
            };

            // Thêm người dùng vào DbContext
            _context.Users.Add(user);
            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            // Trả về kết quả thành công
            return Ok("Đăng ký thành công!");
        }

        /// <summary>
        /// Endpoint để đăng nhập người dùng và tạo JWT / Cookie Session.
        /// </summary>
        /// <param name="userDto">Đối tượng chứa thông tin đăng nhập (Username, Password).</param>
        /// <returns>HTTP 200 OK với JWT nếu đăng nhập thành công, hoặc HTTP 401 Unauthorized nếu thất bại.</returns>
        [HttpPost("login")] // Định nghĩa đây là một phương thức POST với route "api/Auth/login"
        public async Task<IActionResult> Login([FromBody] UserDto userDto)
        {
            // Tìm người dùng trong cơ sở dữ liệu dựa trên tên đăng nhập
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userDto.Username);

            // Kiểm tra nếu không tìm thấy người dùng hoặc mật khẩu không đúng
            if (user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password, user.PasswordHash))
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng.");
            }

            // 1. Tạo JWT (cho các client không dùng session, ví dụ: mobile, SPA)
            var jwtToken = GenerateJwtToken(user);

            // 2. Tạo ClaimsPrincipal và đăng nhập bằng Cookie (cho các client dùng session, ví dụ: trình duyệt)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // ID của người dùng
                new Claim(ClaimTypes.Name, user.Username), // Tên đăng nhập của người dùng
                new Claim("UserEmail", user.Email ?? string.Empty) // Thêm email nếu có
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Đăng nhập người dùng vào scheme CookieAuthenticationDefaults.AuthenticationScheme
            // Điều này sẽ tạo một cookie và gửi về trình duyệt của người dùng.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            // Trả về cả JWT và thông báo đăng nhập thành công
            // Client có thể chọn dùng JWT hoặc dựa vào cookie đã được set
            return Ok(new { Token = jwtToken, Message = "Đăng nhập thành công!" });
        }

        /// <summary>
        /// Phương thức nội bộ để tạo JWT cho người dùng.
        /// </summary>
        /// <param name="user">Đối tượng User đã đăng nhập thành công.</param>
        /// <returns>Chuỗi JWT.</returns>
        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key không được cấu hình trong appsettings.json.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1), // Token hết hạn sau 1 giờ
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    // --- DTO (Data Transfer Object) cho Đăng ký/Đăng nhập ---
    public class UserDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? Email { get; set; }
    }
}