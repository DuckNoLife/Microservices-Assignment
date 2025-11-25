using System.ComponentModel.DataAnnotations;

namespace UserManagement.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        public string? PasswordHash { get; set; }

        // --- MỚI: Thêm Role để phân biệt Admin và User thường ---
        public string Role { get; set; } = "User"; // Mặc định ai đăng ký cũng là User

        public string? GoogleId { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
    }
}