// Services/EmailService.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System; // Thêm dòng này cho InvalidOperationException

namespace QuanLyNguoiDungApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings["SmtpServer"];
            var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var senderEmail = emailSettings["SenderEmail"]; 
            var mailtrapUsername = emailSettings["SenderPassword"]; 
            var mailtrapPassword = _configuration["EmailSettings:MailtrapPassword"]; 
            var senderName = emailSettings["SenderName"] ?? "Support";

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(mailtrapUsername) || string.IsNullOrEmpty(mailtrapPassword))
            {
                throw new InvalidOperationException("Cài đặt email (Mailtrap) chưa được cấu hình đầy đủ trong appsettings.json. Kiểm tra SmtpServer, SenderEmail, SenderPassword (Mailtrap Username) và MailtrapPassword (Mailtrap Password).");
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(senderName, senderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = message };

            using var smtp = new SmtpClient();
            try
            {
                // Kiểm tra xem port có cần SSL/TLS hay không.
                // Mailtrap thường dùng StartTls cho port 2525.
                await smtp.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(mailtrapUsername, mailtrapPassword); // <-- SỬ DỤNG USERNAME VÀ PASSWORD CỦA MAILTRAP Ở ĐÂY
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}