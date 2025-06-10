// Controllers/UsersController.cs
using Microsoft.AspNetCore.Authorization; // Thư viện để sử dụng [Authorize] attribute
using Microsoft.AspNetCore.Mvc; // Thư viện chính cho Controller
using System.Security.Claims; // Để truy cập thông tin Claims từ JWT/Cookie
using QuanLyNguoiDungApi.Data; // Namespace chứa ApplicationDbContext
using Microsoft.EntityFrameworkCore; // Thư viện để sử dụng các phương thức của EF Core (vd: FindAsync)
using Microsoft.AspNetCore.Authentication.JwtBearer; // Để chỉ định scheme JWT
using Microsoft.AspNetCore.Authentication.Cookies; // Để chỉ định scheme Cookie
using Microsoft.AspNetCore.Authentication; // Thư viện để sử dụng HttpContext.SignOutAsync
using System.ComponentModel.DataAnnotations; // Để sử dụng [Required] trong DTO

namespace QuanLyNguoiDungApi.Controllers
{
    // Đánh dấu đây là một Controller API và định nghĩa route mặc định
    [Route("api/[controller]")]
    [ApiController]
    // Mặc định không áp dụng [Authorize] cho toàn bộ Controller để chúng ta có thể
    // chỉ định scheme cụ thể cho từng phương thức (cookie hoặc JWT).
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // Biến để truy cập Database Context

        // Constructor để inject ApplicationDbContext
        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Endpoint để lấy thông tin người dùng đã đăng nhập (từ cookie session).
        /// CHỈ YÊU CẦU XÁC THỰC BẰNG COOKIE SESSION.
        /// Trả về ID, Username, Email, CreatedAt.
        /// </summary>
        /// <returns>HTTP 200 OK cùng với thông tin người dùng an toàn.</returns>
        [HttpGet("me")]
        // Chỉ chấp nhận xác thực bằng Cookie Authentication cho endpoint này.
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetCurrentUser()
        {
            // Lấy ID người dùng từ ClaimsPrincipal (được tạo từ Cookie đã xác thực)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                // Trường hợp hiếm xảy ra nếu cookie hợp lệ nhưng không có ID
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            // Truy vấn database để lấy toàn bộ thông tin người dùng.
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                // Trường hợp hiếm nếu người dùng bị xóa sau khi đăng nhập
                return NotFound("Người dùng không tồn tại.");
            }

            // Trả về DTO an toàn, KHÔNG BAO GỒM PASSWORDHASH
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
            // Lấy ID người dùng từ cookie session đã xác thực
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            // Tìm người dùng trong cơ sở dữ liệu
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            // Cập nhật thông tin Username nếu được cung cấp và có thay đổi
            if (!string.IsNullOrEmpty(updateDto.Username) && user.Username != updateDto.Username)
            {
                // Kiểm tra nếu tên người dùng mới đã tồn tại
                if (await _context.Users.AnyAsync(u => u.Username == updateDto.Username && u.Id != userId))
                {
                    return BadRequest("Tên người dùng mới đã tồn tại.");
                }
                user.Username = updateDto.Username;
            }

            // Cập nhật Email nếu được cung cấp
            if (!string.IsNullOrEmpty(updateDto.Email))
            {
                user.Email = updateDto.Email;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok("Cập nhật thông tin thành công!");
            }
            catch (DbUpdateConcurrencyException)
            {
                // Xử lý xung đột nếu có nhiều request cùng cập nhật (ít xảy ra với một người dùng)
                return Conflict("Đã xảy ra xung đột khi cập nhật. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                // Ghi log lỗi để debug
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
            // Lấy ID người dùng từ cookie session
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            // Tìm người dùng trong cơ sở dữ liệu
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            // Kiểm tra mật khẩu cũ đã nhập với mật khẩu đã băm trong database
            if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.OldPassword, user.PasswordHash))
            {
                return BadRequest("Mật khẩu cũ không đúng.");
            }

            // Băm mật khẩu mới và cập nhật vào database
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);

            await _context.SaveChangesAsync();

            // ĐĂNG XUẤT NGƯỜI DÙNG SAU KHI ĐỔI MẬT KHẨU ĐỂ TĂNG CƯỜNG BẢO MẬT
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok("Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.");
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
            // Lấy ID người dùng từ cookie session
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("Không tìm thấy ID người dùng trong phiên.");
            }
            int userId = int.Parse(userIdClaim.Value);

            // Tìm người dùng trong cơ sở dữ liệu
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại."); // Trường hợp hiếm nếu user đã bị xóa
            }

            // Xóa người dùng khỏi DbContext
            _context.Users.Remove(user);
            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            // Đăng xuất người dùng sau khi xóa tài khoản để hủy phiên hiện tại
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Trả về HTTP 204: Yêu cầu đã được thực hiện thành công, nhưng không có nội dung để trả về.
            return NoContent();
        }

        /// <summary>
        /// Endpoint để lấy thông tin chi tiết của một người dùng bất kỳ theo ID.
        /// YÊU CẦU XÁC THỰC BẰNG JWT.
        /// Chỉ trả về ID, Username, Email và CreatedAt.
        /// </summary>
        /// <param name="id">ID của người dùng cần lấy thông tin.</param>
        /// <returns>HTTP 200 OK với thông tin người dùng, HTTP 404 NotFound nếu không tìm thấy.</returns>
        [HttpGet("{id}")]
        // Chỉ chấp nhận xác thực bằng JWT Bearer scheme cho endpoint này.
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetUserById(int id)
        {
            // Tìm người dùng trong cơ sở dữ liệu bằng ID
            var user = await _context.Users.FindAsync(id);

            // Kiểm tra nếu không tìm thấy người dùng
            if (user == null)
            {
                return NotFound($"Không tìm thấy người dùng với ID: {id}");
            }

            // Trả về các thông tin an toàn của người dùng (KHÔNG BAO GỒM PasswordHash)
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
            // Chọn lọc chỉ những trường cần thiết và an toàn để trả về cho mỗi người dùng
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
                return NotFound("Không có người dùng nào trong hệ thống.");
            }

            return Ok(users);
        }
    }

    // --- DTO (Data Transfer Object) để trả về thông tin chi tiết người dùng an toàn ---
    // Được sử dụng bởi GetCurrentUser()
    public class UserDetailsDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // --- DTO cho việc cập nhật thông tin người dùng ---
    // Được sử dụng bởi UpdateCurrentUser()
    public class UserUpdateDto
    {
        public string? Username { get; set; } // Cho phép null nếu không muốn cập nhật username
        public string? Email { get; set; }    // Cho phép null nếu không muốn cập nhật email
    }

    // --- DTO cho việc thay đổi mật khẩu ---
    // Được sử dụng bởi ChangePassword()
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mật khẩu cũ là bắt buộc.")]
        public string OldPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    // --- DTO MỚI: Để trả về danh sách người dùng (chỉ các trường an toàn) ---
    // Được sử dụng bởi GetAllUsers()
    public class UserDetailsForListDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}