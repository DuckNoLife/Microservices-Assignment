// File: UserManagement/Controllers/AuthController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using UserManagement.Data;
using UserManagement.DTOs;
using UserManagement.Models;
using UserManagement.Services;
using Google.Apis.Auth; // Thư viện vừa cài

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
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Email already exists.");

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Username already exists.");

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordHash = passwordHash,
                Role = "User" // Mặc định là User thường
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

        // 3. ĐĂNG NHẬP GOOGLE (MỚI THÊM)
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin(GoogleLoginRequestDto request)
        {
            try
            {
                // A. Xác thực Token với Server của Google
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);

                // B. Tìm user trong DB xem Email này đã tồn tại chưa
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

                if (user == null)
                {
                    // C. Nếu chưa có -> Tự động ĐĂNG KÝ mới
                    user = new User
                    {
                        Email = payload.Email,
                        // Lấy phần trước @ làm username (ví dụ toan@gmail.com -> toan)
                        Username = payload.Email.Split('@')[0],
                        GoogleId = payload.Subject,
                        Role = "User",
                        PasswordHash = null // User Google không có mật khẩu
                    };

                    // Nếu username bị trùng (ví dụ toan đã có người dùng), thêm số ngẫu nhiên
                    if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                    {
                        user.Username += new Random().Next(1000, 9999).ToString();
                    }

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // D. Nếu đã có -> Cập nhật GoogleId nếu chưa có
                    if (string.IsNullOrEmpty(user.GoogleId))
                    {
                        user.GoogleId = payload.Subject;
                        await _context.SaveChangesAsync();
                    }
                }

                // E. Tạo Token hệ thống trả về cho Frontend
                var token = _tokenService.CreateToken(user);
                return Ok(new { message = "Google login successful", token = token, role = user.Role });
            }
            catch (Exception ex)
            {
                return BadRequest("Invalid Google Token: " + ex.Message);
            }
        }

        // 4. QUÊN MẬT KHẨU
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return BadRequest("User not found.");

            // Không cho reset mật khẩu nếu là tài khoản Google
            if (string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest("Google account cannot reset password.");

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
            user.PasswordResetToken = token;
            user.ResetTokenExpires = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            var resetLink = $"http://localhost:3000/reset-password?token={token}";
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