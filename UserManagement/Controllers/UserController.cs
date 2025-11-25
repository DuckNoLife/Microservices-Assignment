// File: UserManagement/Controllers/UserController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserManagement.Data;
using UserManagement.DTOs;
using UserManagement.Models;

namespace UserManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserDbContext _context;

        public UserController(UserDbContext context)
        {
            _context = context;
        }

        // 1. GET: api/user/all (MỚI: API cho Admin xem danh sách)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")] // Chỉ Admin mới xem được list
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role, // Xem quyền để biết ai là Admin, ai là User
                    u.GoogleId
                })
                .ToListAsync();

            return Ok(users);
        }

        // 2. DELETE: api/user/{id} (Chức năng Xóa User)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Chỉ Admin mới được xóa
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userToDelete = await _context.Users.FindAsync(id);
            if (userToDelete == null) return NotFound("User not found.");

            // LOGIC BẢO VỆ: Không cho phép xóa tài khoản Admin khác
            if (userToDelete.Role == "Admin")
            {
                return BadRequest("You cannot delete another Admin account.");
            }

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User {userToDelete.Username} has been deleted." });
        }

        // ... (Giữ nguyên các hàm GetProfile, UpdateProfile, ChangePassword cũ ở dưới)
        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) return Unauthorized();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");
            return Ok(new { user.Id, user.Username, user.Email, user.GoogleId, user.Role });
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