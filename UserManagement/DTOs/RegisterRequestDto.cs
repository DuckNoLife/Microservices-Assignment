using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")] // <<< Đã trả lại validation này
        public string Password { get; set; } = string.Empty;
    }
}