// File: UserManagement/Services/IEmailService.cs
using System.Threading.Tasks;

namespace UserManagement.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}