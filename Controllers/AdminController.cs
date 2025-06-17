// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuanLyNguoiDungApi.Data;
using QuanLyNguoiDungApi.Models;
using QuanLyNguoiDungApi.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging; // Thêm dòng này
using System.Security.Claims; // Thêm dòng này để truy cập ClaimTypes

namespace QuanLyNguoiDungApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger; // Khai báo ILogger

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger) // Cập nhật constructor
        {
            _context = context;
            _logger = logger; // Gán logger
        }

        // Helper để lấy thông tin Admin thực hiện hành động
        private (string? AdminUsername, string? AdminId) GetAdminInfo()
        {
            var adminUsername = HttpContext.User.Identity?.Name;
            var adminId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return (adminUsername, adminId);
        }

        /// <summary>
        /// Lấy danh sách tất cả người dùng với các vai trò của họ.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        /// <returns>Danh sách người dùng với vai trò.</returns>
        [HttpGet("users-with-roles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã yêu cầu lấy danh sách người dùng với vai trò.", adminUsername, adminId);

            var users = await _context.Users
                .Include(u => u.UserRoles)!
                .ThenInclude(ur => ur.Role)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.IsActive,
                    Roles = u.UserRoles!.Select(ur => ur.Role.Name).ToList(),
                    u.CreatedAt

                })
                .ToListAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã lấy thành công {UserCount} người dùng.", adminUsername, adminId, users.Count);
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
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đang cố gắng gán vai trò '{RoleName}' cho người dùng có ID: {UserId}.", adminUsername, adminId, dto.RoleName, dto.UserId);

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể gán vai trò: Người dùng có ID {UserId} không tồn tại.", adminUsername, adminId, dto.UserId);
                return NotFound("Người dùng không tồn tại.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleName);
            if (role == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể gán vai trò: Vai trò '{RoleName}' không tồn tại.", adminUsername, adminId, dto.RoleName);
                return NotFound("Vai trò không tồn tại.");
            }

            var userHasRole = await _context.UserRoles
                .AnyAsync(ur => ur.UserId == dto.UserId && ur.RoleId == role.Id);

            if (userHasRole)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể gán vai trò: Người dùng '{Username}' (ID: {UserId}) đã có vai trò '{RoleName}'.", adminUsername, adminId, user.Username, dto.UserId, dto.RoleName);
                return BadRequest($"Người dùng '{user.Username}' đã có vai trò '{role.Name}'.");
            }

            var userRole = new UserRole
            {
                UserId = dto.UserId,
                RoleId = role.Id
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã gán vai trò '{RoleName}' thành công cho người dùng '{TargetUsername}' (ID: {TargetUserId}).", adminUsername, adminId, dto.RoleName, user.Username, dto.UserId);
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
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đang cố gắng hủy gán vai trò '{RoleName}' từ người dùng có ID: {UserId}.", adminUsername, adminId, dto.RoleName, dto.UserId);

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể hủy gán vai trò: Người dùng có ID {UserId} không tồn tại.", adminUsername, adminId, dto.UserId);
                return NotFound("Người dùng không tồn tại.");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleName);
            if (role == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể hủy gán vai trò: Vai trò '{RoleName}' không tồn tại.", adminUsername, adminId, dto.RoleName);
                return NotFound("Vai trò không tồn tại.");
            }

            var userRoleToRemove = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == dto.UserId && ur.RoleId == role.Id);

            if (userRoleToRemove == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể hủy gán vai trò: Người dùng '{Username}' (ID: {UserId}) không có vai trò '{RoleName}'.", adminUsername, adminId, user.Username, dto.UserId, dto.RoleName);
                return BadRequest($"Người dùng '{user.Username}' không có vai trò '{role.Name}'.");
            }

            _context.UserRoles.Remove(userRoleToRemove);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã hủy gán vai trò '{RoleName}' thành công từ người dùng '{TargetUsername}' (ID: {TargetUserId}).", adminUsername, adminId, dto.RoleName, user.Username, dto.UserId);
            return Ok($"Vai trò '{role.Name}' đã được hủy gán khỏi người dùng '{user.Username}' thành công.");
        }

        /// <summary>
        /// Lấy danh sách tất cả các vai trò có sẵn trong hệ thống.
        /// Chỉ Admin mới có quyền truy cập.
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã yêu cầu lấy danh sách tất cả các vai trò.", adminUsername, adminId);

            var roles = await _context.Roles.ToListAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã lấy thành công {RoleCount} vai trò.", adminUsername, adminId, roles.Count);
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
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đang cố gắng vô hiệu hóa người dùng có ID: {UserId}.", adminUsername, adminId, userId);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể vô hiệu hóa người dùng: Người dùng có ID {UserId} không tồn tại.", adminUsername, adminId, userId);
                return NotFound("Người dùng không tồn tại.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể vô hiệu hóa người dùng '{TargetUsername}' (ID: {TargetUserId}): Người dùng đã bị vô hiệu hóa.", adminUsername, adminId, user.Username, userId);
                return BadRequest("Người dùng đã bị vô hiệu hóa.");
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã vô hiệu hóa người dùng '{TargetUsername}' (ID: {TargetUserId}) thành công.", adminUsername, adminId, user.Username, userId);
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
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đang cố gắng kích hoạt người dùng có ID: {UserId}.", adminUsername, adminId, userId);

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể kích hoạt người dùng: Người dùng có ID {UserId} không tồn tại.", adminUsername, adminId, userId);
                return NotFound("Người dùng không tồn tại.");
            }

            if (user.IsActive)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể kích hoạt người dùng '{TargetUsername}' (ID: {TargetUserId}): Người dùng đã được kích hoạt.", adminUsername, adminId, user.Username, userId);
                return BadRequest("Người dùng đã được kích hoạt.");
            }

            user.IsActive = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã kích hoạt người dùng '{TargetUsername}' (ID: {TargetUserId}) thành công.", adminUsername, adminId, user.Username, userId);
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
            var (adminUsername, adminId) = GetAdminInfo();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đang cố gắng cập nhật thông tin người dùng có ID: {UserId}.", adminUsername, adminId, dto.UserId);

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể cập nhật người dùng: Người dùng có ID {UserId} không tồn tại.", adminUsername, adminId, dto.UserId);
                return NotFound("Người dùng không tồn tại.");
            }

            bool changed = false;
            var oldUsername = user.Username;
            var oldEmail = user.Email;
            var oldIsActive = user.IsActive;

            if (!string.IsNullOrEmpty(dto.Username) && user.Username != dto.Username)
            {
                if (await _context.Users.AnyAsync(u => u.Username == dto.Username && u.Id != dto.UserId))
                {
                    _logger.LogWarning("Admin '{AdminUsername}' (ID: {AdminId}) không thể cập nhật người dùng '{TargetUsername}' (ID: {TargetUserId}): Tên người dùng mới '{NewUsername}' đã tồn tại.", adminUsername, adminId, user.Username, dto.UserId, dto.Username);
                    return BadRequest("Tên người dùng mới đã tồn tại.");
                }
                user.Username = dto.Username;
                changed = true;
                _logger.LogInformation("Người dùng '{TargetUsername}' (ID: {TargetUserId}) đã thay đổi Username từ '{OldUsername}' thành '{NewUsername}'.", user.Username, dto.UserId, oldUsername, user.Username);
            }

            if (!string.IsNullOrEmpty(dto.Email) && user.Email != dto.Email)
            {
                user.Email = dto.Email;
                changed = true;
                _logger.LogInformation("Người dùng '{TargetUsername}' (ID: {TargetUserId}) đã thay đổi Email từ '{OldEmail}' thành '{NewEmail}'.", user.Username, dto.UserId, oldEmail, user.Email);
            }

            if (dto.IsActive.HasValue && user.IsActive != dto.IsActive.Value)
            {
                user.IsActive = dto.IsActive.Value;
                changed = true;
                _logger.LogInformation("Người dùng '{TargetUsername}' (ID: {TargetUserId}) đã thay đổi trạng thái IsActive từ '{OldIsActive}' thành '{NewIsActive}'.", user.Username, dto.UserId, oldIsActive, user.IsActive);
            }

            if (!changed)
            {
                _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã yêu cầu cập nhật người dùng có ID: {UserId}, nhưng không có thông tin nào được thay đổi.", adminUsername, adminId, dto.UserId);
                return Ok("Không có thông tin nào được cập nhật.");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin '{AdminUsername}' (ID: {AdminId}) đã cập nhật thông tin người dùng '{TargetUsername}' (ID: {TargetUserId}) thành công.", adminUsername, adminId, user.Username, dto.UserId);
            return Ok($"Thông tin người dùng '{user.Username}' đã được cập nhật thành công.");
        }
    }
}