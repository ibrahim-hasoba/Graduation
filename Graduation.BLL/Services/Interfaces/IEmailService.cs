namespace Graduation.BLL.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailVerificationAsync(string email, string firstName, string verificationUrl);
        Task SendVendorApprovalEmailAsync(string email, string storeName, bool isApproved, string? reason = null);
        Task SendPasswordResetEmailAsync(string email, string firstName, string resetUrl);
        Task SendOrderConfirmationEmailAsync(string email, string orderNumber, decimal total);
    }
}