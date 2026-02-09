using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
  public interface IOtpService
  {
    Task<string> GenerateOtpAsync(string email, string purpose = "email_verification", int ttlMinutes = 10);
    Task<bool> ValidateOtpAsync(string email, string code, string purpose = "email_verification");
  }
}
