using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using UserManagement.Data;
using UserManagement.DTOs;
using UserManagement.Models;
using UserManagement.Services;
using Google.Apis.Auth;

namespace UserManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly IUrlShortenerClient _urlShortener; // 👈 1. Khai báo Shortener Service
        private readonly IConfiguration _config;

        // 2. Inject vào Constructor
        public AuthController(UserDbContext context,
                              ITokenService tokenService,
                              IConfiguration config,
                              IEmailService emailService,
                              IUrlShortenerClient urlShortener)
        {
            _context = context;
            _tokenService = tokenService;
            _config = config;
            _emailService = emailService;
            _urlShortener = urlShortener;
        }

        // 1. ĐĂNG KÝ
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            var emailToCheck = request.Email.Trim().ToLower();

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailToCheck))
                return BadRequest("Email already exists.");

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Username already exists.");

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email.Trim(),
                Username = request.Username.Trim(),
                PasswordHash = passwordHash,
                Role = "User"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully!" });
        }

        // 2. ĐĂNG NHẬP THƯỜNG
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == request.UsernameOrEmail ||
                u.Username == request.UsernameOrEmail);

            if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return BadRequest("Invalid username/email or password.");

            string token = _tokenService.CreateToken(user);
            return Ok(new { message = "Login successful!", token = token, role = user.Role });
        }

        // 3. ĐĂNG NHẬP GOOGLE
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request) // Thêm [FromBody] để chắc chắn nhận đúng
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = payload.Email,
                        // Logic lấy phần trước @ làm username của bạn đã đúng ý muốn
                        Username = payload.Email.Split('@')[0],
                        GoogleId = payload.Subject,
                        Role = "User",
                        PasswordHash = null
                    };

                    // Xử lý trùng username bằng cách thêm số ngẫu nhiên
                    if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                    {
                        user.Username += new Random().Next(1000, 9999).ToString();
                    }

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Cập nhật GoogleId nếu user cũ chưa có (trường hợp đã đk bằng email trước đó)
                    if (string.IsNullOrEmpty(user.GoogleId))
                    {
                        user.GoogleId = payload.Subject;
                        await _context.SaveChangesAsync();
                    }
                }

                var token = _tokenService.CreateToken(user);

                // --- PHẦN QUAN TRỌNG ĐÃ SỬA ---
                // Trả về đầy đủ thông tin để Frontend hiển thị
                return Ok(new
                {
                    message = "Google login successful",
                    token = token,
                    role = user.Role,
                    // Thêm 3 trường dưới đây:
                    id = user.Id,            // Cần thiết để gọi API user profile
                    username = user.Username,// Để hiển thị tên (phần trước @)
                    email = user.Email       // Để hiển thị email và fix lỗi "missing email"
                });
                // -----------------------------
            }
            catch (Exception ex)
            {
                return BadRequest("Invalid Google Token: " + ex.Message);
            }
        }

        // 4. QUÊN MẬT KHẨU (ĐÃ NÂNG CẤP: Rút gọn link reset + Fix lỗi 502)
        [HttpPost("/forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Lỗi: Server nhận được Email rỗng.");
            }

            var inputEmail = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == inputEmail);

            if (user == null) return BadRequest($"User not found.");

            if (string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest("Google account cannot reset password.");

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
            user.PasswordResetToken = token;
            user.ResetTokenExpires = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            var frontendUrl = "https://fe-render.onrender.com";
            // Link gốc (dài)
            var longResetLink = $"{frontendUrl}/reset-password?token={token}";

            // --- ĐOẠN CODE SỬA LOGIC Ở ĐÂY ---
            string finalLink = longResetLink; // Mặc định dùng link dài trước
            try
            {
                var shortLink = await _urlShortener.ShortenUrlAsync(longResetLink);

                // Logic: Nếu có link ngắn VÀ link đó không phải là trang HTML lỗi (502 Bad Gateway)
                if (!string.IsNullOrEmpty(shortLink) && !shortLink.Contains("<!DOCTYPE html>"))
                {
                    finalLink = shortLink; // Lúc này mới tin tưởng dùng link ngắn
                }
                else
                {
                    Console.WriteLine("[Warning] Shortener trả về dữ liệu rác hoặc HTML lỗi. Dùng link dài.");
                }
            }
            catch (Exception ex)
            {
                // Nếu lỗi kết nối service shortener thì log ra console và kệ, vẫn gửi link dài
                Console.WriteLine($"[Warning] Lỗi rút gọn link: {ex.Message}. Đang dùng link dài.");
            }
            // ----------------------------------

            // Gửi email chứa link (ngắn hoặc dài)
            await _emailService.SendEmailAsync(user.Email, "Reset Password Request", $"Click here to reset: {finalLink}");

            return Ok(new { message = "Password reset link sent to email." });
        }

        // 5. ĐẶT LẠI MẬT KHẨU
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

            if (user == null || user.ResetTokenExpires < DateTime.UtcNow)
                return BadRequest("Invalid or expired token.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully." });
        }
    }
}