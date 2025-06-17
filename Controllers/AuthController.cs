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
using Microsoft.Extensions.Logging;

namespace QuanLyNguoiDungApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger; 

        public AuthController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService, ILogger<AuthController> logger) // <-- Constructor đã được cập nhật
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }
        // Ví dụ: trong AdminController.cs hoặc một controller khác của bạn
        // Đặt đoạn code này vào một vị trí thích hợp trong class Controller của bạn
        [HttpGet("test-global-error")]
        public IActionResult TestGlobalError()
        {
            int x = 0;
            int y = 1/x; 
            return Ok();
        }
        /// <summary>
        /// Endpoint để đăng ký tài khoản người dùng mới.
        /// </summary>
        /// <param name="request">Đối tượng chứa thông tin đăng ký (Username, Password, Email).</param>
        /// <returns>HTTP 200 OK nếu đăng ký thành công, HTTP 400 BadRequest nếu có lỗi.</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto request)
        {
            _logger.LogInformation("Đang cố gắng đăng ký người dùng: {Username}", request.Username); // Ghi log: Bắt đầu quá trình đăng ký


            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                _logger.LogWarning("Đăng ký không thành công: Tên người dùng '{Username}' đã tồn tại.", request.Username);
                return BadRequest("Tên người dùng đã tồn tại.");
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Tạo đối tượng User mới
            var user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow, 
                IsActive = true 
            };

            // Gán vai trò "User" mặc định 
            _context.Users.Add(user);
            await _context.SaveChangesAsync(); 

            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
            {
                _logger.LogError("Không tìm thấy vai trò 'User' trong cơ sở dữ liệu khi đăng ký người dùng: {Username}. Vui lòng đảm bảo vai trò 'User' đã được thêm vào.", request.Username); // Ghi log: Không tìm thấy vai trò
                return StatusCode(500, "Lỗi nội bộ: Không tìm thấy vai trò 'User' mặc định. Vui lòng liên hệ quản trị viên.");
            }

            var newUserRole = new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id
            };
            _context.UserRoles.Add(newUserRole);
            await _context.SaveChangesAsync(); // Lưu vai trò người dùng

            _logger.LogInformation("User registered successfully: {Username}", request.Username); // Ghi log: Đăng ký thành công
            return Ok("Đăng ký thành công!");
        }

        /// <summary>
        /// Endpoint để đăng nhập người dùng.
        /// Cấp JWT và Cookie Session khi đăng nhập thành công.
        /// </summary>
        /// <param name="request">Đối tượng chứa thông tin đăng nhập (Username, Password).</param>
        /// <returns>HTTP 200 OK với JWT, hoặc HTTP 400 BadRequest nếu thông tin không đúng.</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto request)
        {
            _logger.LogInformation("Cố gắng đăng nhập cho người dùng: {Username}", request.Username); // Ghi log: Bắt đầu đăng nhập

            // Tìm người dùng trong cơ sở dữ liệu và tải các vai trò liên quan
            var user = await _context.Users
                                     .Include(u => u.UserRoles)!
                                     .ThenInclude(ur => ur.Role) 
                                     .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                _logger.LogWarning("Đăng nhập không thành công: Không tìm thấy tên người dùng '{Username}'.", request.Username); // Ghi log: Không tìm thấy người dùng
                return BadRequest("Tên người dùng hoặc mật khẩu không đúng.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("đăng nhập không thành công : Do nhập sai mật khẩu cho người dùng tên  '{Username}'", request.Username); // Ghi log: Sai mật khẩu
                return BadRequest("Tên người dùng hoặc mật khẩu không đúng.");
            }

            // Kiểm tra trạng thái hoạt động của tài khoản
            if (!user.IsActive)
            {
                _logger.LogWarning("Đăng nhập không thành công : Tài khoản  '{Username}'  đang bị khóa .", request.Username); // Ghi log: Tài khoản bị vô hiệu hóa
                return Unauthorized("Tài khoản của bạn đã bị vô hiệu hóa, vui lòng liên hệ admin@gmail.com để mở khóa .");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username!) // Dùng user.Username! để bỏ cảnh báo null nếu bạn chắc chắn nó không null
            };

            // Thêm vai trò vào claims
            if (user.UserRoles != null)
            {
                foreach (var userRole in user.UserRoles)
                {
                    if (userRole.Role != null && !string.IsNullOrEmpty(userRole.Role.Name))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
                    }
                }
            }

            // Tạo và cấp JWT
            var token = GenerateJwtToken(claims);

            // Tạo và cấp Cookie Session (truyền claims đã có vai trò)
            // Lưu ý: Nếu bạn chỉ dùng JWT cho API thì phần Cookie này có thể không cần thiết
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Lưu cookie ngay cả khi đóng trình duyệt
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30), // Hạn sử dụng của cookie
                AllowRefresh = true // Cho phép refresh cookie khi hết hạn một phần
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            _logger.LogInformation("Người dùng'{Username}'  đã đăng  nhập thành công.", request.Username); // Ghi log: Đăng nhập thành công
            return Ok(new { Token = token, Message = "Đăng nhập thành công!" });
        }

        /// <summary>
        /// Endpoint để người dùng đăng xuất.
        /// </summary>
        /// <returns>HTTP 200 OK nếu đăng xuất thành công.</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Người dùng đã yêu cầu đăng xuất.đã xóa côkie.");
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
            _logger.LogInformation("Có yêu cầu cấp lại mật khẩu từ email: {Email}", request.Email); // Ghi log: Yêu cầu quên mật khẩu

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                // Để tránh lộ thông tin email nào tồn tại, chúng ta luôn trả về OK
                // nhưng không gửi email nếu email không tồn tại.
                _logger.LogWarning("Yêu cầu mật khẩu cho emai không tồn tại trong hệ thống: {Email}=>> Không có email được guier đi.", request.Email); 
                return Ok("Nếu email tồn tại, một liên kết đặt lại mật khẩu đã được gửi đi.");
            }

            // Tạo một token reset mật khẩu duy nhất
            var token = Guid.NewGuid().ToString("N"); // Tạo GUID , bỏ dấu gạch ngang
            var expiresAt = DateTime.UtcNow.AddHours(1); 

            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            };

            // Xóa các token cũ chưa sử dụng của người dùng này để tránh lộn xộn
            var existingTokens = await _context.PasswordResetTokens
                                               .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                                               .ToListAsync();
            _context.PasswordResetTokens.RemoveRange(existingTokens);

            _context.PasswordResetTokens.Add(passwordResetToken);
            await _context.SaveChangesAsync();

            // Xây dựng liên kết reset mật khẩu
            // Sử dụng Url.Action để tạo URL động
            var resetLink = Url.Action(nameof(ResetPasswordConfirm), "Auth", new { email = user.Email, token = token }, Request.Scheme);

            var subject = "Đặt lại mật khẩu của bạn";
            var message = $"Chào {user.Username},<br/><br/>" +
                          $"Bạn đã yêu cầu đặt lại mật khẩu. Vui lòng nhấp vào liên kết sau để đặt lại mật khẩu của bạn:<br/>" +
                          $"<a href='{resetLink}'>Đặt lại mật khẩu</a><br/><br/>" +
                          $"Liên kết này sẽ hết hạn trong 1 giờ. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.<br/><br/>" +
                          $"Trân trọng,<br/>" +
                          $"Hệ thống quản lý người dùng";

            // Gửi email
            try
            {
                await _emailService.SendEmailAsync(user.Email!, subject, message);
                _logger.LogInformation("Đa gửi emaik cấp kại mật khẩu tới email: {Email}.", user.Email); // Ghi log: Gửi email thành công
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể gửi được mail cấp lại mật khẩu tới {Email}.", user.Email); // Ghi log lỗi: Không gửi được email
               
                return Ok("Nếu email tồn tại, một liên kết đặt lại mật khẩu đã được gửi đi.");
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
            _logger.LogInformation("Xác nhận đặt lại mật khẩu cho email: {Email}", request.Email); // Ghi log: Bắt đầu xác nhận reset

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                _logger.LogWarning("Xác nhận đặt lại mật khẩu không thành công: Người dùng '{Email}' không tồn tại.", request.Email); // Ghi log: Người dùng không tồn tại
                return NotFound("Người dùng không tồn tại.");
            }

            // Tìm token hợp lệ
            var tokenEntry = await _context.PasswordResetTokens
                                           .Where(t => t.UserId == user.Id && t.Token == request.Token)
                                           .OrderByDescending(t => t.CreatedAt) // Lấy token mới nhất nếu có nhiều
                                           .FirstOrDefaultAsync();

            if (tokenEntry == null)
            {
                _logger.LogWarning("Cấp lại mật khẩu không thành công  cho người dùng có email: '{Email}': Token không đúng .", request.Email); // Ghi log: Token không hợp lệ
                return BadRequest("Token không hợp lệ hoặc không tìm thấy.");
            }

            if (tokenEntry.IsUsed)
            {
                _logger.LogWarning("Cấp lại mật khẩu không thành công  cho người dùng có email:'{Email}': Token đã được sử dụng.", request.Email); // Ghi log: Token đã sử dụng
                return BadRequest("Token đã được sử dụng.");
            }

            if (tokenEntry.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Cấp lại mật khẩu không thành công  cho người dùng có email:'{Email}':  Token Đã hết hạn.", request.Email); // Ghi log: Token hết hạn
                return BadRequest("Token đã hết hạn.");
            }

       
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            tokenEntry.IsUsed = true; // Đánh dấu token đã được sử dụng

            await _context.SaveChangesAsync();

            _logger.LogInformation("Mật khấu của người dùng '{Email}' đã được cấp lại thành công.", request.Email);
            return Ok("Mật khẩu của bạn đã được đặt lại thành công.");
        }

        /// <summary>
        /// Endpoint để kiểm tra quyền truy cập khi bị từ chối.
        /// </summary>
        [HttpGet("accessdenied")]
        public IActionResult AccessDenied()
        {
            _logger.LogWarning("Quyền truy cập bị từ chối: Người dùng đã cố gắng truy cập vào tài nguyên trái phép."); 
            return Forbid("Bạn không có quyền truy cập tài nguyên này.");
        }

        /// <summary>
        /// Phương thức nội bộ để tạo JWT cho người dùng.
        /// </summary>
        /// <param name="claims">Danh sách các claims của người dùng.</param>
        /// <returns>Chuỗi JWT.</returns>
        private string GenerateJwtToken(List<Claim> claims)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                _logger.LogError("JWT Key không được cấu hình trong appsettings.json."); // Ghi log lỗi: Key JWT không cấu hình
                throw new InvalidOperationException("JWT Key không được cấu hình trong appsettings.json.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), 
                Expires = DateTime.UtcNow.AddMinutes(120), 
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}