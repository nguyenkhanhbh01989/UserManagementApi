// Controllers/UsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuanLyNguoiDungApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;
using QuanLyNguoiDungApi.DTOs;
using Microsoft.Extensions.Logging; 

namespace QuanLyNguoiDungApi.Controllers
{
    // Đánh dấu đây là một Controller API và định nghĩa route mặc định
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context; 
        private readonly ILogger<UsersController> _logger; // 

        // Constructor để inject ApplicationDbContext và ILogger
        public UsersController(ApplicationDbContext context, ILogger<UsersController> logger) 
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint để lấy thông tin người dùng đã đăng nhập (từ cookie session).
        /// CHỈ YÊU CẦU XÁC THỰC BẰNG COOKIE SESSION.
        /// Trả về ID, Username, Email, CreatedAt.
        /// </summary>
        /// <returns>HTTP 200 OK cùng với thông tin người dùng an toàn.</returns>
        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetCurrentUser()
        {
            _logger.LogInformation("Yêu cầu lấy thông tin người dùng hiện tại.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("Không tìm thấy ID người dùng trong phiên khi lấy thông tin người dùng hiện tại.");
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                _logger.LogError("Người dùng với ID '{UserId}' không tồn tại trong DB dù có phiên hợp lệ.", userId);
                return NotFound("Người dùng không tồn tại.");
            }

            _logger.LogInformation("Đã lấy thông tin người dùng hiện tại thành công cho ID: '{UserId}'.", userId);
            return Ok(new UserDetailsDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            });
        }

        /// <summary>
        /// Endpoint để cập nhật thông tin cá nhân của người dùng đã đăng nhập.
        /// Chỉ chấp nhận xác thực bằng Cookie Session.
        /// Có thể cập nhật Username và Email.
        /// </summary>
        /// <param name="updateDto">Đối tượng chứa thông tin cập nhật (Username, Email).</param>
        /// <returns>HTTP 200 OK nếu cập nhật thành công, HTTP 400 BadRequest nếu có lỗi.</returns>
        [HttpPut("me")] // Phương thức PUT với route "api/Users/me"
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateCurrentUser([FromBody] UserUpdateDto updateDto)
        {
            _logger.LogInformation("Yêu cầu cập nhật thông tin người dùng hiện tại.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("Không tìm thấy ID người dùng trong phiên khi cập nhật thông tin.");
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError("Người dùng với ID '{UserId}' không tồn tại trong DB khi cập nhật thông tin.", userId);
                return NotFound("Người dùng không tồn tại.");
            }

            // Ghi lại thông tin cũ trước khi cập nhật
            _logger.LogInformation("Thông tin người dùng hiện tại (ID: {UserId}) trước cập nhật: Username='{UsernameCu}', Email='{EmailCu}'.",
                userId, user.Username, user.Email);

            bool changed = false;
            if (!string.IsNullOrEmpty(updateDto.Username) && user.Username != updateDto.Username)
            {
                if (await _context.Users.AnyAsync(u => u.Username == updateDto.Username && u.Id != userId))
                {
                    _logger.LogWarning("Cập nhật thất bại: Tên người dùng '{UsernameMoi}' đã tồn tại cho người dùng ID '{UserId}'.", updateDto.Username, userId);
                    return BadRequest("Tên người dùng mới đã tồn tại.");
                }
                user.Username = updateDto.Username;
                changed = true;
                _logger.LogInformation("Người dùng ID '{UserId}' đã cập nhật Tên người dùng thành '{UsernameMoi}'.", userId, updateDto.Username);
            }

            if (!string.IsNullOrEmpty(updateDto.Email) && user.Email != updateDto.Email)
            {
                user.Email = updateDto.Email;
                changed = true;
                _logger.LogInformation("Người dùng ID '{UserId}' đã cập nhật Email thành '{EmailMoi}'.", userId, updateDto.Email);
            }

            if (!changed)
            {
                _logger.LogInformation("Không có thông tin nào được thay đổi cho người dùng ID '{UserId}'.", userId);
                return Ok("Không có thông tin nào được cập nhật.");
            }

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cập nhật thông tin người dùng ID '{UserId}' thành công.", userId);
                return Ok("Cập nhật thông tin thành công!");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Lỗi đồng thời khi cập nhật thông tin người dùng ID '{UserId}'.", userId);
                return Conflict("Đã xảy ra xung đột khi cập nhật. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi cập nhật thông tin người dùng ID '{UserId}'.", userId);
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        /// <summary>
        /// Endpoint để thay đổi mật khẩu của người dùng đã đăng nhập.
        /// Chỉ chấp nhận xác thực bằng Cookie Session.
        /// Yêu cầu mật khẩu cũ và mật khẩu mới.
        /// SAU KHI ĐỔI MẬT KHẨU THÀNH CÔNG, HỆ THỐNG SẼ ĐĂNG XUẤT NGƯỜI DÙNG.
        /// </summary>
        /// <param name="changePasswordDto">Đối tượng chứa mật khẩu cũ và mới.</param>
        /// <returns>HTTP 200 OK nếu thay đổi thành công, HTTP 400 BadRequest nếu có lỗi.</returns>
        [HttpPost("me/change-password")] // Route riêng cho việc thay đổi mật khẩu
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            _logger.LogInformation("Yêu cầu thay đổi mật khẩu cho người dùng hiện tại.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("Không tìm thấy ID người dùng trong phiên khi thay đổi mật khẩu.");
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError("Người dùng với ID '{UserId}' không tồn tại trong DB khi thay đổi mật khẩu.", userId);
                return NotFound("Người dùng không tồn tại.");
            }

            if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.OldPassword, user.PasswordHash))
            {
                _logger.LogWarning("Thay đổi mật khẩu thất bại cho người dùng ID '{UserId}': Mật khẩu cũ không đúng.", userId);
                return BadRequest("Mật khẩu cũ không đúng.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Mật khẩu của người dùng ID '{UserId}' đã được thay đổi thành công.", userId);

                // ĐĂNG XUẤT NGƯỜI DÙNG SAU KHI ĐỔI MẬT KHẨU ĐỂ TĂNG CƯỜNG BẢO MẬT
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _logger.LogInformation("Người dùng ID '{UserId}' đã được đăng xuất sau khi đổi mật khẩu.", userId);

                return Ok("Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi thay đổi mật khẩu cho người dùng ID '{UserId}'.", userId);
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        /// <summary>
        /// Endpoint để xóa tài khoản của người dùng đã đăng nhập.
        /// Chỉ chấp nhận xác thực bằng Cookie Session.
        /// </summary>
        /// <returns>HTTP 204 No Content nếu xóa thành công, HTTP 401 Unauthorized.</returns>
        [HttpDelete("me")] // Phương thức DELETE với route "api/Users/me"
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteCurrentUser()
        {
            _logger.LogInformation("Yêu cầu xóa tài khoản của người dùng hiện tại.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("Không tìm thấy ID người dùng trong phiên khi xóa tài khoản.");
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError("Người dùng với ID '{UserId}' không tồn tại trong DB khi cố gắng xóa tài khoản.", userId);
                return NotFound("Người dùng không tồn tại."); // Trường hợp hiếm nếu user đã bị xóa
            }

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Tài khoản của người dùng ID '{UserId}' đã được xóa thành công.", userId);

                // Đăng xuất người dùng sau khi xóa tài khoản để hủy phiên hiện tại
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _logger.LogInformation("Người dùng ID '{UserId}' đã được đăng xuất sau khi xóa tài khoản.", userId);

                return NoContent(); // HTTP 204: Yêu cầu đã được thực hiện thành công, nhưng không có nội dung để trả về.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi xóa tài khoản người dùng ID '{UserId}'.", userId);
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        /// <summary>
        /// Endpoint để lấy thông tin chi tiết của một người dùng bất kỳ theo ID.
        /// YÊU CẦU XÁC THỰC BẰNG JWT.
        /// Chỉ trả về ID, Username, Email và CreatedAt.
        /// </summary>
        /// <param name="id">ID của người dùng cần lấy thông tin.</param>
        /// <returns>HTTP 200 OK với thông tin người dùng, HTTP 404 NotFound nếu không tìm thấy.</returns>
        [HttpGet("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetUserById(int id)
        {
            _logger.LogInformation("Yêu cầu lấy thông tin người dùng với ID: {Id}.", id);

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với ID: {Id}.", id);
                return NotFound($"Không tìm thấy người dùng với ID: {id}");
            }

            _logger.LogInformation("Đã lấy thông tin người dùng với ID '{Id}' thành công.", id);
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.CreatedAt
            });
        }

        /// <summary>
        /// Endpoint để lấy danh sách thông tin của TẤT CẢ người dùng.
        /// CHỈ YÊU CẦU XÁC THỰC BẰNG JWT.
        /// Chỉ trả về ID, Username, Email; chỉ xem, không thêm/sửa/xóa.
        /// </summary>
        /// <returns>HTTP 200 OK với danh sách người dùng.</returns>
        [HttpGet] // Route mặc định của Controller: /api/Users
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<IEnumerable<UserDetailsForListDto>>> GetAllUsers()
        {
            _logger.LogInformation("Yêu cầu lấy danh sách tất cả người dùng.");

            var users = await _context.Users
                                    .Select(u => new UserDetailsForListDto
                                    {
                                        Id = u.Id,
                                        Username = u.Username,
                                        Email = u.Email
                                    })
                                    .ToListAsync();

            if (users == null || !users.Any())
            {
                _logger.LogInformation("Không có người dùng nào trong hệ thống.");
                return NotFound("Không có người dùng nào trong hệ thống.");
            }

            _logger.LogInformation("Đã lấy thành công {SoLuongNguoiDung} người dùng.", users.Count);
            return Ok(users);
        }
    }
}