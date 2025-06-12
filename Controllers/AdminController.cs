// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyNguoiDungApi.Data;
using QuanLyNguoiDungApi.Models;
using QuanLyNguoiDungApi.DTOs; 
using Microsoft.AspNetCore.Authentication.JwtBearer; 

namespace QuanLyNguoiDungApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Chỉ cho phép người dùng có vai trò "Admin" truy cập Controller này
    // Chúng ta dùng JwtBearerDefaults.AuthenticationScheme vì các API Admin thường được gọi từ ứng dụng client
    // bằng JWT token, không phải cookie session.
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách tất cả người dùng với các vai trò của họ.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <returns>Danh sách người dùng với vai trò.</returns>
        [HttpGet("users-with-roles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var users = await _context.Users
                .Include(u => u.UserRoles)!
                .ThenInclude(ur => ur.Role)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    Roles = u.UserRoles!.Select(ur => ur.Role.Name).ToList()
                })
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>
        /// Gán một vai trò cho người dùng.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <param name="dto">Đối tượng chứa UserId và RoleName.</param>
        /// <returns>HTTP 200 OK nếu thành công, HTTP 400 BadRequest nếu lỗi, HTTP 404 Not Found nếu không tìm thấy User/Role.</returns>
        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] UserRoleUpdateDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleName);
            if (role == null)
            {
                return NotFound("Vai trò không tồn tại.");
            }

            // Kiểm tra xem người dùng đã có vai trò này chưa để tránh thêm trùng lặp
            var userHasRole = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == dto.UserId && ur.RoleId == role.Id);

            if (userHasRole)
            {
                return BadRequest($"Người dùng '{user.Username}' đã có vai trò '{role.Name}'.");
            }

            // Gán vai trò
            var userRole = new UserRole
            {
                UserId = dto.UserId,
                RoleId = role.Id
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            return Ok($"Vai trò '{role.Name}' đã được gán cho người dùng '{user.Username}' thành công.");
        }

        /// <summary>
        /// Hủy gán một vai trò từ người dùng.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <param name="dto">Đối tượng chứa UserId và RoleName.</param>
        /// <returns>HTTP 200 OK nếu thành công, HTTP 400 BadRequest nếu lỗi, HTTP 404 Not Found nếu không tìm thấy User/Role.</returns>
        [HttpPost("remove-role")]
        public async Task<IActionResult> RemoveRole([FromBody] UserRoleUpdateDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleName);
            if (role == null)
            {
                return NotFound("Vai trò không tồn tại.");
            }

            // Tìm và xóa vai trò khỏi người dùng
            var userRoleToRemove = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == dto.UserId && ur.RoleId == role.Id);

            if (userRoleToRemove == null)
            {
                return BadRequest($"Người dùng '{user.Username}' không có vai trò '{role.Name}'.");
            }

            _context.UserRoles.Remove(userRoleToRemove);
            await _context.SaveChangesAsync();

            return Ok($"Vai trò '{role.Name}' đã được hủy gán khỏi người dùng '{user.Username}' thành công.");
        }

        /// <summary>
        /// Lấy danh sách tất cả các vai trò có sẵn trong hệ thống.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            return Ok(roles);
        }
        /// <summary>
        /// Vô hiệu hóa một tài khoản người dùng.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <param name="userId">ID của người dùng cần vô hiệu hóa.</param>
        /// <returns>HTTP 200 OK nếu thành công, HTTP 404 Not Found nếu không tìm thấy người dùng.</returns>
        [HttpPost("disable-user/{userId}")]
        public async Task<IActionResult> DisableUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            if (!user.IsActive)
            {
                return BadRequest("Người dùng đã bị vô hiệu hóa.");
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok($"Người dùng '{user.Username}' đã bị vô hiệu hóa.");
        }

        /// <summary>
        /// Kích hoạt một tài khoản người dùng đã bị vô hiệu hóa.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <param name="userId">ID của người dùng cần kích hoạt.</param>
        /// <returns>HTTP 200 OK nếu thành công, HTTP 404 Not Found nếu không tìm thấy người dùng.</returns>
        [HttpPost("enable-user/{userId}")]
        public async Task<IActionResult> EnableUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            if (user.IsActive)
            {
                return BadRequest("Người dùng đã được kích hoạt.");
            }

            user.IsActive = true;
            await _context.SaveChangesAsync();
            return Ok($"Người dùng '{user.Username}' đã được kích hoạt.");
        }

        /// <summary>
        /// Cập nhật thông tin cơ bản của người dùng bởi Admin.
        /// Chỉ Admin mới có quyền truy cập.
        /// (Không cho phép thay đổi mật khẩu trực tiếp qua API này)
        /// </summary>
        /// <param name="dto">Đối tượng chứa UserId và các thông tin cần cập nhật (Username, Email).</param>
        /// <returns>HTTP 200 OK nếu thành công, HTTP 400 BadRequest nếu lỗi, HTTP 404 Not Found nếu không tìm thấy người dùng.</returns>
        [HttpPut("update-user")]
        public async Task<IActionResult> AdminUpdateUser([FromBody] AdminUserUpdateDto dto)
        {
            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            bool changed = false;

            // Cập nhật Username nếu được cung cấp và khác với giá trị hiện tại
            if (!string.IsNullOrEmpty(dto.Username) && user.Username != dto.Username)
            {
                // Kiểm tra xem Username mới đã tồn tại chưa
                if (await _context.Users.AnyAsync(u => u.Username == dto.Username && u.Id != dto.UserId))
                {
                    return BadRequest("Tên người dùng mới đã tồn tại.");
                }
                user.Username = dto.Username;
                changed = true;
            }

            // Cập nhật Email nếu được cung cấp và khác với giá trị hiện tại
            if (!string.IsNullOrEmpty(dto.Email) && user.Email != dto.Email)
            {
                user.Email = dto.Email;
                changed = true;
            }

            // thay đổi trạng thái IsActive 
            if (dto.IsActive.HasValue && user.IsActive != dto.IsActive.Value)
            {
                user.IsActive = dto.IsActive.Value;
                changed = true;
            }

            if (!changed)
            {
                return Ok("Không có thông tin nào được cập nhật.");
            }

            await _context.SaveChangesAsync();
            return Ok($"Thông tin người dùng '{user.Username}' đã được cập nhật thành công.");
        }
    }
}