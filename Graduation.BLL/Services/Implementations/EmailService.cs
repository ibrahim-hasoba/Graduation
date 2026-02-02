using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace Graduation.BLL.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _senderName;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpServer = _configuration["EmailSettings:SmtpServer"]!;
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            _senderEmail = _configuration["EmailSettings:SenderEmail"]!;
            _senderPassword = _configuration["EmailSettings:SenderPassword"]!;
            _senderName = _configuration["EmailSettings:SenderName"]!;
        }

        public async Task SendEmailVerificationAsync(string email, string firstName, string verificationUrl)
        {
            var subject = "Verify Your Email - Egyptian Products Marketplace";

            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .button {{ display: inline-block; padding: 15px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🇪🇬 Welcome to Egyptian Products Marketplace!</h1>
                        </div>
                        <div class='content'>
                            <h2>Hello {firstName}! 👋</h2>
                            <p>Thank you for registering with Egyptian Products Marketplace - your gateway to authentic Egyptian products!</p>
                            <p>To complete your registration and verify your email address, please click the button below:</p>
                            <div style='text-align: center;'>
                                <a href='{verificationUrl}' class='button'>Verify Email Address</a>
                            </div>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='background: #fff; padding: 10px; border: 1px solid #ddd; word-break: break-all;'>{verificationUrl}</p>
                            <p><strong>This link will expire in 24 hours.</strong></p>
                            <p>If you didn't create an account, you can safely ignore this email.</p>
                            <div class='footer'>
                                <p>© 2025 Egyptian Products Marketplace. Made with ❤️ in Egypt 🇪🇬</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(email, subject, body);
        }

        public async Task SendVendorApprovalEmailAsync(string email, string storeName, bool isApproved, string? reason = null)
        {
            var subject = isApproved
                ? "🎉 Your Vendor Account Has Been Approved!"
                : "Vendor Application Update";

            var body = isApproved
                ? $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #10b981;'>Congratulations! 🎉</h2>
                            <p>Your vendor account for <strong>{storeName}</strong> has been approved!</p>
                            <p>You can now start adding products and selling on our platform.</p>
                            <p>Login to your account and start your journey as an Egyptian products vendor!</p>
                        </div>
                    </body>
                    </html>
                "
                : $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2>Vendor Application Update</h2>
                            <p>Unfortunately, your vendor application for <strong>{storeName}</strong> was not approved at this time.</p>
                            {(string.IsNullOrEmpty(reason) ? "" : $"<p><strong>Reason:</strong> {reason}</p>")}
                            <p>You can update your application and reapply if you'd like.</p>
                        </div>
                    </body>
                    </html>
                ";

            await SendEmailAsync(email, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string email, string firstName, string resetUrl)
        {
            var subject = "Reset Your Password - Egyptian Products Marketplace";

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2>Password Reset Request</h2>
                        <p>Hello {firstName},</p>
                        <p>We received a request to reset your password. Click the link below to set a new password:</p>
                        <p><a href='{resetUrl}' style='display: inline-block; padding: 10px 20px; background: #667eea; color: white; text-decoration: none; border-radius: 5px;'>Reset Password</a></p>
                        <p>This link will expire in 1 hour.</p>
                        <p>If you didn't request this, please ignore this email.</p>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(email, subject, body);
        }

        public async Task SendOrderConfirmationEmailAsync(string email, string orderNumber, decimal total)
        {
            var subject = $"Order Confirmation - {orderNumber}";

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2>Order Confirmed! 🎉</h2>
                        <p>Thank you for your order!</p>
                        <p><strong>Order Number:</strong> {orderNumber}</p>
                        <p><strong>Total Amount:</strong> {total:N2} EGP</p>
                        <p>We'll notify you when your order is shipped.</p>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(email, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using var smtpClient = new SmtpClient(_smtpServer, _smtpPort)
                {
                    Credentials = new NetworkCredential(_senderEmail, _senderPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail, _senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - email failures shouldn't break the app
                // TODO: Add proper logging
                Console.WriteLine($"Email sending failed: {ex.Message}");
            }
        }
    }
}