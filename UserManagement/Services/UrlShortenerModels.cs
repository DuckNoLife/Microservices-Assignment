namespace UserManagement.Services
{
    // Class này dùng để hứng kết quả trả về từ Service kia
    public class ShortUrlResponse
    {
        public string ShortLink { get; set; }
        public string ShortCode { get; set; }
    }
}