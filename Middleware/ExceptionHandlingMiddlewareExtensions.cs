// Middleware/ExceptionHandlingMiddlewareExtensions.cs
using Microsoft.AspNetCore.Builder;

namespace QuanLyNguoiDungApi.Middleware
{
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}