using MailKit.Net.Smtp;
using MailKit.Security; // BẮT BUỘC CÓ
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
        // 1. Lấy thông tin từ cấu hình
        var emailSettings = _configuration.GetSection("EmailSettings");
        var emailFrom = emailSettings["Email"];
        var password = emailSettings["Password"]; // App Password

        // 2. Cấu hình cứng cho Gmail (Dùng Port 465 + SSL để fix lỗi Timeout)
        var host = "smtp.gmail.com";
        var port = 465;

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
            // Tăng thời gian chờ lên 20s
            smtp.Timeout = 20000;

            Console.WriteLine($"[Gmail Log] Connecting to {host}:{port}...");

            // QUAN TRỌNG: Dùng SslOnConnect cho Port 465
            await smtp.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);

            Console.WriteLine("[Gmail Log] Authenticating...");
            await smtp.AuthenticateAsync(emailFrom, password);

            Console.WriteLine("[Gmail Log] Sending...");
            await smtp.SendAsync(email);

            Console.WriteLine("[Gmail Log] --> SUCCESS! Email sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gmail Log] ERROR: {ex.Message}");
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}