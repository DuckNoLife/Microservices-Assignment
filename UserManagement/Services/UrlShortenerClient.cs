using System.Net.Http.Json; //  Bắt buộc phải có dòng này để dùng PostAsJsonAsync

namespace UserManagement.Services
{
    public interface IUrlShortenerClient
    {
        Task<string?> ShortenUrlAsync(string originalUrl);
    }

    public class UrlShortenerClient : IUrlShortenerClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<UrlShortenerClient> _logger;

        public UrlShortenerClient(IHttpClientFactory httpClientFactory, ILogger<UrlShortenerClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> ShortenUrlAsync(string originalUrl)
        {
            try
            {
                // 1. Lấy Client đã cấu hình trong Program.cs
                var client = _httpClientFactory.CreateClient("UrlShortenerService");

                // 2. Tạo cục dữ liệu để gửi đi (Đúng tên OriginalUrl như bên kia yêu cầu)
                var payload = new { OriginalUrl = originalUrl };

                // 3. Gửi POST request
                // Đường dẫn là "api/Shortener/shorten" (nối đuôi vào base url)
                var response = await client.PostAsJsonAsync("api/Shortener/shorten", payload);

                if (response.IsSuccessStatusCode)
                {
                    // 4. Đọc kết quả trả về
                    var result = await response.Content.ReadFromJsonAsync<ShortUrlResponse>();
                    return result?.ShortLink; // Trả về link rút gọn (VD: https://shorten.../code/abc)
                }
                else
                {
                    // Nếu lỗi thì log ra để biết đường sửa
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Lỗi khi gọi URL Shortener: {response.StatusCode} - {errorContent}");
                    return null; // Hoặc trả về chính originalUrl nếu muốn fallback
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception khi gọi URL Shortener: {ex.Message}");
                return null;
            }
        }
    }
}