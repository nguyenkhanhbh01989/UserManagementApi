// Middleware/ExceptionHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using QuanLyNguoiDungApi.DTOs; 

namespace QuanLyNguoiDungApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env; // Thêm IHostEnvironment

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env) // Cập nhật constructor
        {
            _next = next;
            _logger = logger;
            _env = env; // Gán IHostEnvironment
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                // Ghi lại lỗi chi tiết vào log
                _logger.LogError(ex, "Đã xảy ra một ngoại lệ không được xử lý: {Message}", ex.Message);
                await HandleExceptionAsync(httpContext, ex, _env.IsDevelopment()); // Truyền thêm thông tin môi trường
            }
        }

        // Cập nhật phương thức HandleExceptionAsync để nhận thêm tham số isDevelopment
        private static Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // Lỗi không xác định => 500

            string message = "Đã có lỗi xảy ra trong quá trình xử lý yêu cầu.";
            string? details = null;

            // Chỉ hiển thị chi tiết lỗi (stack trace) trong môi trường phát triển
            if (isDevelopment)
            {
                details = exception.StackTrace?.ToString();
                message += " Vui lòng thử lại sau hoặc liên hệ quản trị viên."; // Thêm thông báo thân thiện hơn
            }
            else
            {
                // Trong môi trường sản phẩm, không hiển thị chi tiết lỗi cho client
                // Chỉ hiển thị thông báo chung đã được định nghĩa ở trên
            }

            // Sử dụng lớp ErrorResponse DTO đã tạo
            var errorResponse = new ErrorResponse(context.Response.StatusCode, message, details);

            // Serialize đối tượng phản hồi thành JSON và ghi vào Response
            // Sử dụng JsonSerializerOptions để chuyển đổi sang camelCase cho Property
            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(jsonResponse);
        }
    }
}