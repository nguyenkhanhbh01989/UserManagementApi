// Services/IEmailService.cs
using System.Threading.Tasks;

namespace QuanLyNguoiDungApi.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
    }
}