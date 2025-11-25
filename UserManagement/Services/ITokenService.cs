using UserManagement.Models;

namespace UserManagement.Services // <<< Sửa thành UserManagement
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}