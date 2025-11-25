using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class ResetPasswordRequestDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")] // <<< Validate độ dài
        public string NewPassword { get; set; } = string.Empty;
    }
}