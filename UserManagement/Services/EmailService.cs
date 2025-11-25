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
        // 1. Lấy thông tin từ Render Environment
        var emailSettings = _configuration.GetSection("EmailSettings");
        var emailFrom = emailSettings["Email"];    // Sẽ lấy: Taolaita789@outlook.com
        var password = emailSettings["Password"];  // Sẽ lấy: ppadffohngomimlc

        // 2. Cấu hình Server OUTLOOK
        var host = "smtp.office365.com";
        var port = 587;

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(emailFrom));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        var builder = new BodyBuilder();
        builder.HtmlBody = body;
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            smtp.Timeout = 20000; // 20 giây

            Console.WriteLine($"[Outlook] Kết nối đến {host}:{port} bằng {emailFrom}...");

            // BẮT BUỘC: Outlook dùng StartTls
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);

            Console.WriteLine("[Outlook] Đang đăng nhập...");
            await smtp.AuthenticateAsync(emailFrom, password);

            Console.WriteLine("[Outlook] Đang gửi mail...");
            await smtp.SendAsync(email);

            Console.WriteLine("[Outlook] --> GỬI THÀNH CÔNG!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Outlook LỖI] {ex.Message}");
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}