using UserManagement.Models;

namespace UserManagement.Services 
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}