using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using UserManagement.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // 1. Đọc cấu hình từ appsettings.json
        var emailSettings = _configuration.GetSection("EmailSettings");
        var mail = emailSettings["Email"];
        var pw = emailSettings["Password"];
        var host = emailSettings["Host"];
        // Đảm bảo đọc Port là số int, nếu lỗi thì fallback về 587
        var port = int.TryParse(emailSettings["Port"], out int p) ? p : 587;

        // 2. Khởi tạo SmtpClient
        var client = new SmtpClient(host, port)
        {
            EnableSsl = true, // Gmail bắt buộc
            Credentials = new NetworkCredential(mail, pw),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(mail),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mailMessage.To.Add(to);

        // 3. Gửi mail
        try
        {
            await client.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            // Log lỗi ra để debug nếu cần
            Console.WriteLine($"Gửi mail thất bại: {ex.Message}");
            throw;
        }
    }
}