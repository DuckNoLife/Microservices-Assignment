// File: UserManagement/Services/EmailService.cs
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace UserManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var emailFrom = _config["EmailSettings:Email"];
            var password = _config["EmailSettings:Password"];
            var host = _config["EmailSettings:Host"];

            // Port lưu trong json là chuỗi, cần parse ra int
            if (string.IsNullOrEmpty(_config["EmailSettings:Port"]))
            {
                // Giá trị mặc định là 587 nếu quên cấu hình
                throw new Exception("Chưa cấu hình Port email");
            }
            var port = int.Parse(_config["EmailSettings:Port"]!);

            using (var client = new SmtpClient(host, port))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(emailFrom, password);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(emailFrom!),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);
                await client.SendMailAsync(mailMessage);
            }
        }
    }
}