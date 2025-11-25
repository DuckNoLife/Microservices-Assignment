using System.ComponentModel.DataAnnotations;

namespace UserManagement.DTOs
{
    public class UpdateProfileDto
    {
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        // Có thể thêm AvatarUrl, Bio... nếu muốn mở rộng sau này
    }
}