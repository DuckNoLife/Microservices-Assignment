using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")] // <<< Validate chuẩn Email
        public string Email { get; set; } = string.Empty;
    }
}