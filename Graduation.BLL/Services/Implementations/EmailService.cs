using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Graduation.BLL.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _senderName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _smtpServer = _configuration["EmailSettings:SmtpServer"]
                ?? throw new InvalidOperationException("EmailSettings:SmtpServer is not configured");
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]
                ?? throw new InvalidOperationException("EmailSettings:SmtpPort is not configured"));
            _senderEmail = _configuration["EmailSettings:SenderEmail"]
                ?? throw new InvalidOperationException("EmailSettings:SenderEmail is not configured");
            _senderPassword = _configuration["EmailSettings:SenderPassword"]
                ?? throw new InvalidOperationException("EmailSettings:SenderPassword is not configured");
            _senderName = _configuration["EmailSettings:SenderName"]
                ?? throw new InvalidOperationException("EmailSettings:SenderName is not configured");
        }

        private string BuildHtml(string contentHtml)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Heka</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f4f2ee; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f2ee; padding: 24px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; width: 100%;"">
                    <tr>
                        <td style=""padding: 0 16px;"">
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 12px rgba(0,0,0,0.06);"">
                                <tr>
                                    <td style=""background: linear-gradient(135deg, #c75b39 0%, #a84428 100%); padding: 32px 40px; text-align: center;"">
                                        <h1 style=""margin: 0; font-size: 28px; color: #ffffff; letter-spacing: 1px; font-weight: 700;"">HEKA</h1>
                                        <p style=""margin: 4px 0 0; font-size: 13px; color: rgba(255,255,255,0.8); font-weight: 300;"">Egyptian Products Marketplace</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding: 40px;"">
                                        {contentHtml}
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""background-color: #f9f7f5; padding: 24px 40px; text-align: center; border-top: 1px solid #e8e2dc;"">
                                        <p style=""margin: 0 0 6px; font-size: 13px; color: #8a7a6e;"">Heka &mdash; Egyptian Products Marketplace</p>
                                        <p style=""margin: 0; font-size: 12px; color: #b0a296;"">If you have any questions, contact our support team.</p>
                                        <p style=""margin: 4px 0 0; font-size: 12px; color: #b0a296;"">&copy; {DateTime.UtcNow.Year} Heka. All rights reserved.</p>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin: 12px 0 0; font-size: 11px; color: #b0a296; text-align: center;"">This is an automated message from Heka. Please do not reply directly.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string BuildOtpContent(string firstName, string code)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Verify your email</h2>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Hello {firstName},</p>
                <p style=""margin: 0 0 24px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Use the code below to confirm your email address and activate your account.</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin: 0 auto 24px;"">
                    <tr>
                        <td style=""background-color: #faf6f2; border: 2px dashed #c75b39; border-radius: 12px; padding: 18px 40px; letter-spacing: 8px; font-size: 32px; font-weight: 700; color: #c75b39; text-align: center;"">{code}</td>
                    </tr>
                </table>
                <p style=""margin: 0 0 4px; font-size: 13px; color: #8a7a6e;"">This code expires in <strong>10 minutes</strong>. Do not share it with anyone.</p>
                <p style=""margin: 0; font-size: 13px; color: #8a7a6e;"">If you didn&rsquo;t request this, you can safely ignore this email.</p>
            ";
        }

        private string BuildVendorApprovedContent(string storeName)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Welcome to Heka!</h2>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Good news &mdash; your vendor account has been approved!</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%; margin-bottom: 24px;"">
                    <tr>
                        <td style=""background-color: #f0faf4; border: 1px solid #b8e6cc; border-radius: 8px; padding: 16px 20px;"">
                            <p style=""margin: 0; font-size: 15px; color: #1a7a42;"">
                                <strong>Store:</strong> {storeName}<br>
                                <strong>Status:</strong> <span style=""color: #1a7a42; font-weight: 600;"">Approved</span>
                            </p>
                        </td>
                    </tr>
                </table>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">You can now add products, manage inventory, and start selling on Heka. Log in to your dashboard to get started.</p>
                <p style=""margin: 0; font-size: 14px; color: #8a7a6e;"">Welcome aboard!</p>
            ";
        }

        private string BuildVendorRejectedContent(string storeName, string? reason)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Application Update</h2>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Thank you for your interest in selling on Heka.</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%; margin-bottom: 24px;"">
                    <tr>
                        <td style=""background-color: #fef6f0; border: 1px solid #f5d6c2; border-radius: 8px; padding: 16px 20px;"">
                            <p style=""margin: 0; font-size: 15px; color: #a85c3a;"">
                                <strong>Store:</strong> {storeName}<br>
                                <strong>Status:</strong> <span style=""color: #c75b39; font-weight: 600;"">Not Approved</span>
                            </p>
                        </td>
                    </tr>
                </table>
                {(string.IsNullOrEmpty(reason) ? "" : $@"<p style=""margin: 0 0 16px; font-size: 15px; color: #5c524a; line-height: 1.6;""><strong>Reason:</strong> {reason}</p>")}
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">You can update your application details and reapply at any time. If you have questions, contact our support team.</p>
                <p style=""margin: 0; font-size: 14px; color: #8a7a6e;"">We appreciate your interest!</p>
            ";
        }

        private string BuildPasswordResetContent(string firstName, string resetUrl)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Reset your password</h2>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Hello {firstName},</p>
                <p style=""margin: 0 0 24px; font-size: 15px; color: #5c524a; line-height: 1.6;"">We received a request to reset your password. Click the button below to set a new one.</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin: 0 auto 24px;"">
                    <tr>
                        <td style=""background: linear-gradient(135deg, #c75b39 0%, #a84428 100%); border-radius: 8px; text-align: center;"">
                            <a href=""{resetUrl}"" style=""display: inline-block; padding: 14px 36px; font-size: 15px; font-weight: 600; color: #ffffff; text-decoration: none;"">Reset Password</a>
                        </td>
                    </tr>
                </table>
                <p style=""margin: 0 0 4px; font-size: 13px; color: #8a7a6e;"">This link expires in <strong>1 hour</strong>.</p>
                <p style=""margin: 0; font-size: 13px; color: #8a7a6e;"">If you didn&rsquo;t request a password reset, please ignore this email.</p>
            ";
        }

        private string BuildOrderConfirmationContent(string orderNumber, decimal total)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Order confirmed!</h2>
                <p style=""margin: 0 0 24px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Thank you for your purchase. Your order has been placed successfully.</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%; margin-bottom: 24px;"">
                    <tr>
                        <td style=""background-color: #f9f7f5; border-radius: 8px; padding: 20px;"">
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td style=""padding-bottom: 10px; font-size: 14px; color: #8a7a6e;"">Order Number</td>
                                    <td style=""padding-bottom: 10px; font-size: 14px; color: #2d2a26; text-align: right; font-weight: 600;"">{orderNumber}</td>
                                </tr>
                                <tr>
                                    <td style=""padding-bottom: 10px; font-size: 14px; color: #8a7a6e; border-bottom: 1px solid #e8e2dc;"">Total Amount</td>
                                    <td style=""padding-bottom: 10px; font-size: 14px; color: #c75b39; text-align: right; font-weight: 700; border-bottom: 1px solid #e8e2dc;"">{total:N2} EGP</td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
                <p style=""margin: 0 0 4px; font-size: 15px; color: #5c524a; line-height: 1.6;"">We&rsquo;ll notify you when your order ships. You can track its status from your account dashboard.</p>
                <p style=""margin: 0; font-size: 14px; color: #8a7a6e;"">Thank you for shopping at Heka!</p>
            ";
        }

        private string BuildVerificationWarningContent(string firstName, int hoursRemaining)
        {
            return $@"
                <h2 style=""margin: 0 0 8px; font-size: 22px; color: #2d2a26; font-weight: 700;"">Action required</h2>
                <p style=""margin: 0 0 20px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Hi {firstName},</p>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width: 100%; margin-bottom: 24px;"">
                    <tr>
                        <td style=""background-color: #fef6f0; border: 1px solid #f5d6c2; border-radius: 8px; padding: 16px 20px;"">
                            <p style=""margin: 0; font-size: 15px; color: #a85c3a;"">Your account will be deleted in <strong>{hoursRemaining} hours</strong> if you don&rsquo;t verify your email.</p>
                        </td>
                    </tr>
                </table>
                <p style=""margin: 0 0 4px; font-size: 15px; color: #5c524a; line-height: 1.6;"">Please check your inbox for the verification code we sent earlier, or request a new one from your account settings.</p>
                <p style=""margin: 0; font-size: 14px; color: #8a7a6e;"">If you need help, contact our support team.</p>
            ";
        }

        public async Task SendEmailOtpAsync(string email, string firstName, string code)
        {
            var body = BuildHtml(BuildOtpContent(firstName, code));
            await SendEmailAsync(email, "Verify your email - Heka", body);
        }

        public async Task SendVendorApprovalEmailAsync(string email, string storeName, bool isApproved, string? reason = null)
        {
            var subject = isApproved
                ? "Your vendor account has been approved - Heka"
                : "Vendor application update - Heka";

            var content = isApproved
                ? BuildVendorApprovedContent(storeName)
                : BuildVendorRejectedContent(storeName, reason);

            var body = BuildHtml(content);
            await SendEmailAsync(email, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string email, string firstName, string resetUrl)
        {
            var body = BuildHtml(BuildPasswordResetContent(firstName, resetUrl));
            await SendEmailAsync(email, "Reset your password - Heka", body);
        }

        public async Task SendOrderConfirmationEmailAsync(string email, string orderNumber, decimal total)
        {
            var body = BuildHtml(BuildOrderConfirmationContent(orderNumber, total));
            await SendEmailAsync(email, $"Order confirmed - {orderNumber}", body);
        }

        public async Task SendVerificationWarningEmailAsync(string email, string firstName, int hoursRemaining)
        {
            var body = BuildHtml(BuildVerificationWarningContent(firstName, hoursRemaining));
            await SendEmailAsync(email, "Action required: verify your email - Heka", body);
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
                _logger.LogError(ex, "Email sending failed");
            }
        }
    }
}