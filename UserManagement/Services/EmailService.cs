using MailKit.Net.Smtp; // Lưu ý: Dùng của MailKit, không dùng System.Net.Mail
using Microsoft.Extensions.Configuration;
using MimeKit;
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
        // Đọc config
        var emailSettings = _configuration.GetSection("EmailSettings");
        var emailFrom = emailSettings["Email"];    // Email gửi (Gmail của bạn)
        var password = emailSettings["Password"];  // App Password 16 ký tự
        var host = "smtp.gmail.com";
        var port = 587;

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(emailFrom));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        // Tạo nội dung email (HTML)
        var builder = new BodyBuilder();
        builder.HtmlBody = body;
        email.Body = builder.ToMessageBody();

        // Gửi email bằng MailKit
        using var smtp = new SmtpClient();
        try
        {
            // Connect: host, port, useSsl (false cho port 587 vì nó dùng StartTls)
            // MailKit thông minh hơn, nó sẽ tự switch sang SecureSocketOptions.StartTls
            await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);

            // Authenticate
            await smtp.AuthenticateAsync(emailFrom, password);

            // Send
            await smtp.SendAsync(email);

            Console.WriteLine("--> Gửi email thành công!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--> Lỗi gửi mail MailKit: {ex.Message}");
            throw; // Ném lỗi để Controller biết
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}