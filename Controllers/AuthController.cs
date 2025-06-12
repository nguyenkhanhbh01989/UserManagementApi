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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Generic;
using QuanLyNguoiDungApi.DTOs; 

namespace QuanLyNguoiDungApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // Biến db Context
        private readonly IConfiguration _configuration;

        // Constructor để inject ApplicationDbContext và IConfiguration
        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Endpoint để đăng ký tài khoản người dùng mới.
        /// </summary>
        /// <param name="request">Đối tượng chứa thông tin đăng ký (Username, Password, Email).</param>
        /// <returns>HTTP 200 OK nếu đăng ký thành công, HTTP 400 BadRequest nếu có lỗi.</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto request) // Sử dụng UserRegisterDto
        {
            // Kiểm tra xem Username đã tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Tên người dùng đã tồn tại.");
            }

            // Băm mật khẩu trước khi lưu trữ
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Tạo đối tượng User mới
            var user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow // Ghi lại thời gian tạo
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(); // <-- QUAN TRỌNG: Lưu người dùng để có Id trước khi gán vai trò

            // --- BẮT ĐẦU: THÊM PHẦN GÁN VAI TRÒ MẶC ĐỊNH LÀ "User" ---
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
            {
                // Xử lý trường hợp không tìm thấy vai trò 'User' (có thể do lỗi seed data)
                return StatusCode(500, "Lỗi server: Không tìm thấy vai trò 'User' mặc định. Vui lòng liên hệ quản trị viên.");
            }

            var newUserRole = new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id
            };
            _context.UserRoles.Add(newUserRole);
            await _context.SaveChangesAsync(); // <-- QUAN TRỌNG: Lưu UserRole vào DB
            // --- KẾT THÚC: THÊM PHẦN GÁN VAI TRÒ MẶC ĐỊNH ---

            return Ok("Đăng ký thành công!");
        }

        /// <summary>
        /// Endpoint để đăng nhập người dùng.
        /// Cấp JWT và Cookie Session khi đăng nhập thành công.
        /// </summary>
        /// <param name="request">Đối tượng chứa thông tin đăng nhập (Username, Password).</param>
        /// <returns>HTTP 200 OK với JWT, hoặc HTTP 400 BadRequest nếu thông tin không đúng.</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto request) // Sử dụng UserLoginDto
        {
            // Tìm người dùng trong cơ sở dữ liệu và tải các vai trò liên quan
            var user = await _context.Users
                                     .Include(u => u.UserRoles)! // Đảm bảo tải các vai trò liên quan
                                     .ThenInclude(ur => ur.Role) // Đảm bảo tải thông tin chi tiết về vai trò
                                     .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                return BadRequest("Tên người dùng hoặc mật khẩu không đúng.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return BadRequest("Tên người dùng hoặc mật khẩu không đúng.");
            }

            if (!user.IsActive)
            {
                return Unauthorized("Tài khoản của bạn đã bị vô hiệu hóa.");
            }

           
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
              //them neu cần 
            };

            // Thêm vai trò vào claims
            if (user.UserRoles != null)
            {
                foreach (var userRole in user.UserRoles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
                }
            }
            // --- KẾT THÚC: CẬP NHẬT CLAIMS ---

            // Tạo và cấp JWT (truyền claims đã có vai trò)
            var token = GenerateJwtToken(claims);

            // Tạo và cấp Cookie Session (truyền claims đã có vai trò)
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Lưu cookie ngay cả khi đóng trình duyệt
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30), // Hạn sử dụng của cookie
                AllowRefresh = true // Cho phép refresh cookie khi hết hạn một phần
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            return Ok(new { Token = token, Message = "Đăng nhập thành công!" });
        }

        /// <summary>
        /// Endpoint để người dùng đăng xuất.
        /// </summary>
        /// <returns>HTTP 200 OK nếu đăng xuất thành công.</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Đăng xuất khỏi Cookie Authentication scheme
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok("Đăng xuất thành công.");
        }

        /// <summary>
        /// Phương thức nội bộ để tạo JWT cho người dùng.
        /// </summary>
        /// <param name="claims">Danh sách các claims của người dùng.</param>
        /// <returns>Chuỗi JWT.</returns>
        private string GenerateJwtToken(List<Claim> claims) // Đã sửa để nhận List<Claim>
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key không được cấu hình trong appsettings.json.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), // Sử dụng claims đã được truyền vào
                Expires = DateTime.UtcNow.AddHours(1), // Token hết hạn sau 1 giờ
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Endpoint để kiểm tra quyền truy cập khi bị từ chối.
        /// </summary>
        [HttpGet("accessdenied")]
        public IActionResult AccessDenied()
        {
            // Trả về Forbid để báo hiệu rằng người dùng đã xác thực nhưng không có quyền.
            return Forbid("Bạn không có quyền truy cập tài nguyên này.");
        }
    }
}