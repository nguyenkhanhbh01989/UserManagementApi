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
using QuanLyNguoiDungApi.Services;

namespace QuanLyNguoiDungApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService; 

        public AuthController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService) // CẬP NHẬT CONSTRUCTOR
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService; 
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
        /// Yêu cầu reset mật khẩu.
        /// Gửi email chứa token reset mật khẩu đến người dùng.
        /// </summary>
        /// <param name="request">Đối tượng chứa email của người dùng.</param>
        /// <returns>HTTP 200 OK nếu yêu cầu được xử lý, HTTP 400 BadRequest nếu email không tồn tại.</returns>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return Ok("Nếu email tồn tại, một liên kết đặt lại mật khẩu đã được gửi đi.");
            }

            // Tạo một token reset mật khẩu duy nhất
            var token = Guid.NewGuid().ToString("N"); // Tạo GUID và loại bỏ dấu gạch ngang
            var expiresAt = DateTime.UtcNow.AddHours(1);

            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            };

            // Xóa các token cũ chưa sử dụng của người dùng này 
            var existingTokens = await _context.PasswordResetTokens
                                               .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                                               .ToListAsync();
            _context.PasswordResetTokens.RemoveRange(existingTokens);

            _context.PasswordResetTokens.Add(passwordResetToken);
            await _context.SaveChangesAsync();

            // Xây dựng liên kết reset mật khẩu
            // ví dụ: "https://yourfrontend.com/reset-password?email={user.Email}&token={token}"
            var resetLink = Url.Action(nameof(ResetPasswordConfirm), "Auth", new { email = user.Email, token = token }, Request.Scheme);
            // var resetLink = $"http://localhost:5000/reset-password?email={user.Email}&token={token}";

            var subject = "Đặt lại mật khẩu của bạn";
            var message = $"Chào {user.Username},<br/><br/>" +
                          $"Bạn đã yêu cầu đặt lại mật khẩu. Vui lòng nhấp vào liên kết sau để đặt lại mật khẩu của bạn:<br/>" +
                          $"<a href='{resetLink}'>Đặt lại mật khẩu</a><br/><br/>" +
                          $"Liên kết này sẽ hết hạn trong 1 giờ. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.<br/><br/>" +
                          $"Trân trọng,<br/>" +
                          $"Lần sau nhớ mk nhé cu.";

            // Gửi email
            try
            {
                await _emailService.SendEmailAsync(user.Email!, subject, message);
            }
            catch (Exception ex)
            {
                // Log lỗi gửi email nhưng vẫn trả về OK để tránh lộ thông tin
                Console.WriteLine($"Error sending email: {ex.Message}");
                // return StatusCode(500, "Có lỗi xảy ra khi gửi email đặt lại mật khẩu."); // Chỉ trả về khi muốn lộ lỗi gửi email
            }

            return Ok("Nếu email tồn tại, một liên kết đặt lại mật khẩu đã được gửi đi.");
        }

        /// <summary>
        /// Xác nhận và đặt mật khẩu mới sau khi người dùng đã yêu cầu reset.
        /// </summary>
        /// <param name="request">Đối tượng chứa Email, Token và NewPassword.</param>
        /// <returns>HTTP 200 OK nếu mật khẩu được đặt lại thành công, hoặc HTTP 400 BadRequest/404 Not Found nếu lỗi.</returns>
        [HttpPost("reset-password-confirm")]
        public async Task<IActionResult> ResetPasswordConfirm([FromBody] ResetPasswordConfirmDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            // Tìm token hợp lệ
            var tokenEntry = await _context.PasswordResetTokens
                                           .Where(t => t.UserId == user.Id && t.Token == request.Token)
                                           .OrderByDescending(t => t.CreatedAt) // Lấy token mới nhất nếu có nhiều
                                           .FirstOrDefaultAsync();

            if (tokenEntry == null)
            {
                return BadRequest("Token không hợp lệ hoặc không tìm thấy.");
            }

            if (tokenEntry.IsUsed)
            {
                return BadRequest("Token đã được sử dụng.");
            }

            if (tokenEntry.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest("Token đã hết hạn.");
            }

            // Mọi thứ hợp lệ, đặt lại mật khẩu
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            tokenEntry.IsUsed = true; 

            await _context.SaveChangesAsync();

            return Ok("Mật khẩu của bạn đã được đặt lại thành công.");
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