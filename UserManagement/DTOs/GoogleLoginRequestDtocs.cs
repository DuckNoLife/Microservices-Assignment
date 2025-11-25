using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class GoogleLoginRequestDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }
}