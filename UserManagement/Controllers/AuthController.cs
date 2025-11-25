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
        private readonly IConfiguration _config;

        public AuthController(UserDbContext context, ITokenService tokenService, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _tokenService = tokenService;
            _config = config;
            _emailService = emailService;
        }

        // 1. ĐĂNG KÝ
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            // Chuẩn hóa input để tránh lỗi trùng lặp do hoa/thường
            var emailToCheck = request.Email.Trim().ToLower();

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailToCheck))
                return BadRequest("Email already exists.");

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Username already exists.");

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email, // Lưu nguyên bản (hoặc lưu emailToCheck tùy bạn)
                Username = request.Username,
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
        public async Task<IActionResult> GoogleLogin(GoogleLoginRequestDto request)
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
                        Username = payload.Email.Split('@')[0],
                        GoogleId = payload.Subject,
                        Role = "User",
                        PasswordHash = null
                    };

                    if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                    {
                        user.Username += new Random().Next(1000, 9999).ToString();
                    }

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    if (string.IsNullOrEmpty(user.GoogleId))
                    {
                        user.GoogleId = payload.Subject;
                        await _context.SaveChangesAsync();
                    }
                }

                var token = _tokenService.CreateToken(user);
                return Ok(new { message = "Google login successful", token = token, role = user.Role });
            }
            catch (Exception ex)
            {
                return BadRequest("Invalid Google Token: " + ex.Message);
            }
        }

        // 4. QUÊN MẬT KHẨU (ĐÃ SỬA LOGIC CHECK LỖI)
        [HttpPost("/forgot-password")] // Giữ nguyên dấu / ở đầu để khớp với frontend
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            // -- DEBUG LOG -- (Xem trong Render Logs)
            Console.WriteLine($"[DEBUG] ForgotPassword called. Raw Email: '{request?.Email}'");

            // 1. Kiểm tra dữ liệu đầu vào
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Lỗi: Server nhận được Email rỗng. Kiểm tra lại Frontend (biến phải tên là 'email').");
            }

            // 2. Chuẩn hóa chuỗi (Cắt khoảng trắng + Chữ thường)
            var inputEmail = request.Email.Trim().ToLower();

            // 3. Tìm kiếm trong DB (So sánh chữ thường để tránh lỗi PostgreSQL case-sensitive)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == inputEmail);

            // 4. Báo lỗi chi tiết nếu không tìm thấy
            if (user == null)
            {
                Console.WriteLine($"[DEBUG] Không tìm thấy user nào khớp với: {inputEmail}");
                return BadRequest($"User not found. (Server đã tìm email: '{inputEmail}' nhưng không thấy)");
            }

            // 5. Kiểm tra tài khoản Google
            if (string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest("Google account cannot reset password.");

            // 6. Tạo Token và Gửi Mail
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
            user.PasswordResetToken = token;
            user.ResetTokenExpires = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            var frontendUrl = "https://fe-render.onrender.com";
            var resetLink = $"{frontendUrl}/reset-password?token={token}";

            await _emailService.SendEmailAsync(user.Email, "Reset Password Request", $"Click here: {resetLink}");

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
