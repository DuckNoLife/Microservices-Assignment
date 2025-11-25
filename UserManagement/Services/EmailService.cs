using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json; // Cần cái này để bắn JSON
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
        // 1. Lấy API Key từ cấu hình (Vẫn dùng cái mã xsmtpsib-... cũ)
        var emailSettings = _configuration.GetSection("EmailSettings");
        var apiKey = emailSettings["Password"];

        // 2. Cấu hình gửi qua API (Không dùng SMTP nữa nên không lo bị chặn Port)
        var url = "https://api.brevo.com/v3/smtp/email";

        // Sender phải là email bạn đã verify trong Brevo (Email cá nhân của bạn)
        // Tôi để cứng email Gmail của bạn ở đây để chắc chắn chạy
        var senderEmail = "taolaita789@gmail.com";
        var senderName = "Forgot password";

        // 3. Tạo cục dữ liệu JSON theo chuẩn của Brevo API
        var payload = new
        {
            sender = new { name = senderName, email = senderEmail },
            to = new[] { new { email = to } },
            subject = subject,
            htmlContent = body
        };

        // 4. Bắn Request HTTP
        using var client = new HttpClient();

        // Thêm Key vào Header
        client.DefaultRequestHeaders.Add("api-key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            Console.WriteLine($"[Brevo API] Sending to {to}...");

            var response = await client.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[Brevo API] --> SUCCESS! Email sent.");
            }
            else
            {
                // Nếu lỗi thì đọc nội dung lỗi từ Brevo trả về
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Brevo API ERROR] Status: {response.StatusCode}, Details: {errorContent}");
                throw new Exception($"Gửi mail thất bại: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Brevo API CRASH] {ex.Message}");
            throw;
        }
    }
}