using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Threading.Tasks;
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
        // 1. Lấy thông tin từ cấu hình (Render Environment hoặc appsettings.json)
        var emailSettings = _configuration.GetSection("EmailSettings");

        // QUAN TRỌNG: Lấy thông tin đăng nhập
        var emailLogin = emailSettings["Email"];    // Đây là cái mail lạ lạ (9c8522...)
        var password = emailSettings["Password"];   // Đây là mã Key dài (xsmtpsib...)

        // Sender Name: Khi gửi mail sẽ hiện tên này (Bạn có thể sửa lại)
        var senderName = "Do An Tot Nghiep";
        // Sender Email: Brevo yêu cầu email gửi (From) phải là email bạn đã Verify
        // (Là cái email taolaita789@gmail.com trong Profile của bạn)
        var senderEmail = "taolaita789@gmail.com";

        // 2. CẤU HÌNH CỨNG CHO BREVO (Để đảm bảo không bao giờ sai)
        var host = "smtp-relay.brevo.com";
        var port = 587;

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(senderName, senderEmail));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        var builder = new BodyBuilder();
        builder.HtmlBody = body;
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            smtp.Timeout = 10000; // 10 giây

            Console.WriteLine($"[Brevo SMTP] Connecting to {host}:{port}...");

            // BẮT BUỘC: Brevo dùng StartTls
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);

            Console.WriteLine($"[Brevo SMTP] Authenticating as {emailLogin}...");

            // Đăng nhập bằng tài khoản Login riêng của SMTP
            await smtp.AuthenticateAsync(emailLogin, password);

            Console.WriteLine("[Brevo SMTP] Sending...");
            await smtp.SendAsync(email);

            Console.WriteLine("[Brevo SMTP] --> SUCCESS! Email sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Brevo ERROR] {ex.Message}");
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}