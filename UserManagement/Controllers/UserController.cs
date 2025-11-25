using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserManagement.Data;
using UserManagement.DTOs;
using UserManagement.Models;
using UserManagement.Services; // 👈 Nhớ using namespace này

namespace UserManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserDbContext _context;
        private readonly IUrlShortenerClient _urlShortener; // 👈 1. Khai báo service

        // 2. Tiêm service vào Constructor
        public UserController(UserDbContext context, IUrlShortenerClient urlShortener)
        {
            _context = context;
            _urlShortener = urlShortener;
        }

        // 1. GET: api/user/all (Admin xem danh sách)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.GoogleId
                })
                .ToListAsync();

            return Ok(users);
        }

        // 2. DELETE: api/user/{id} (Xóa User)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userToDelete = await _context.Users.FindAsync(id);
            if (userToDelete == null) return NotFound("User not found.");

            if (userToDelete.Role == "Admin")
            {
                return BadRequest("You cannot delete another Admin account.");
            }

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User {userToDelete.Username} has been deleted." });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            return Ok(new { user.Id, user.Username, user.Email, user.GoogleId, user.Role });
        }

        // 👇 [TÍNH NĂNG MỚI] Tạo Link giới thiệu rút gọn cho User
        // User gọi API này -> Hệ thống gọi sang Service Shortener -> Trả về link ngắn
        [HttpPost("create-referral-link")]
        public async Task<IActionResult> CreateReferralLink()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            // 1. Giả sử link giới thiệu gốc (dài) trỏ về Frontend
            // Ví dụ: https://fe-render.onrender.com/register?ref=123
            string longUrl = $"https://fe-render.onrender.com/register?ref={userIdString}";

            // 2. Gọi sang Service bên kia để rút gọn
            string? shortUrl = await _urlShortener.ShortenUrlAsync(longUrl);

            // 3. Kiểm tra kết quả
            if (string.IsNullOrEmpty(shortUrl))
            {
                // Nếu bên kia lỗi, trả về link gốc luôn (fallback)
                return Ok(new { link = longUrl, note = "Service rút gọn đang bận, dùng link gốc tạm nhé." });
            }

            return Ok(new { link = shortUrl });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) return Unauthorized();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (request.Username != user.Username)
            {
                if (await _context.Users.AnyAsync(u => u.Username == request.Username)) return BadRequest("Username taken.");
                user.Username = request.Username;
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated", username = user.Username });
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) return Unauthorized();
            var user = await _context.Users.FindAsync(userId);
            if (string.IsNullOrEmpty(user.PasswordHash)) return BadRequest("Google users cannot change password.");
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)) return BadRequest("Incorrect password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password changed." });
        }
    }
}