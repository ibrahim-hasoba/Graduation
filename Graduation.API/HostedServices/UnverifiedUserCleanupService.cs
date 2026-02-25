using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Graduation.BLL.Services.Interfaces;

namespace Graduation.API.HostedServices
{
    /// <summary>
    /// Periodically removes user accounts that were created but never had their email
    /// verified within the configured grace period.
    ///
    /// FIX #10 changes:
    ///   1. Grace period raised from 24 h to 72 h (configurable via
    ///      UnverifiedUserCleanup:GracePeriodHours). This reduces the chance of deleting
    ///      legitimate users whose confirmation email was slow or went to spam.
    ///   2. Before deletion, a "final warning" email is sent at the 48-hour mark giving the
    ///      user 24 more hours and a direct link to resend the verification email.
    ///   3. Users who have no OTP record at all (meaning our OTP system never successfully
    ///      sent them a code) are skipped — deleting them would hide an email-delivery bug.
    ///   4. Service run interval is configurable via UnverifiedUserCleanup:RunIntervalHours.
    /// </summary>
    public class UnverifiedUserCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnverifiedUserCleanupService> _logger;
        private readonly TimeSpan _runInterval;
        private readonly int _gracePeriodHours;
        private readonly int _warningThresholdHours; // send warning email after this many hours

        public UnverifiedUserCleanupService(
            IServiceProvider serviceProvider,
            ILogger<UnverifiedUserCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _gracePeriodHours = configuration.GetValue<int>("UnverifiedUserCleanup:GracePeriodHours", 72);
            var runIntervalHours = configuration.GetValue<int>("UnverifiedUserCleanup:RunIntervalHours", 6);
            _runInterval = TimeSpan.FromHours(runIntervalHours);

            // Send warning email halfway through grace period
            _warningThresholdHours = _gracePeriodHours / 2;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "UnverifiedUserCleanupService started. Grace period: {GracePeriod}h, " +
                "Warning threshold: {Warning}h, Run interval: {Interval}",
                _gracePeriodHours, _warningThresholdHours, _runInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_runInterval, stoppingToken);

                try
                {
                    await RunCleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during unverified user cleanup");
                }
            }
        }

        private async Task RunCleanupAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.UtcNow;
            var graceCutoff = now.AddHours(-_gracePeriodHours);
            var warningCutoff = now.AddHours(-_warningThresholdHours);

            // FIX #10: Only target users for whom we have evidence that an OTP was actually
            // sent. Skip accounts with no OTP record — they indicate a broken email delivery
            // on our side, not user inaction.
            var candidateUsers = await context.Users
                .Where(u => !u.EmailConfirmed
                         && u.CreatedAt <= warningCutoff
                         && context.EmailOtps.Any(otp => otp.Email == u.Email))
                .ToListAsync(ct);

            var toDelete = new List<AppUser>();
            var toWarn = new List<AppUser>();

            foreach (var user in candidateUsers)
            {
                if (user.CreatedAt <= graceCutoff)
                {
                    toDelete.Add(user);
                }
                else if (!user.WarningEmailSentAt.HasValue)
                {
                    // Reached the halfway warning mark but not yet the deletion cutoff
                    toWarn.Add(user);
                }
            }

            // Send warning emails
            foreach (var user in toWarn)
            {
                try
                {
                    await emailService.SendVerificationWarningEmailAsync(
                        user.Email!,
                        user.FirstName,
                        hoursRemaining: _gracePeriodHours - _warningThresholdHours);

                    user.WarningEmailSentAt = now;
                    await userManager.UpdateAsync(user);

                    _logger.LogInformation(
                        "Sent verification warning email to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send warning email to {Email}", user.Email);
                }
            }

            // Delete expired unverified accounts
            int deleted = 0;
            foreach (var user in toDelete)
            {
                try
                {
                    var result = await userManager.DeleteAsync(user);
                    if (result.Succeeded)
                    {
                        deleted++;
                        _logger.LogInformation(
                            "Deleted unverified account {Email} (created {CreatedAt}, " +
                            "grace period of {Hours}h exceeded)",
                            user.Email, user.CreatedAt, _gracePeriodHours);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to delete unverified account {Email}: {Errors}",
                            user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception deleting unverified account {Email}", user.Email);
                }
            }

            _logger.LogInformation(
                "Cleanup run complete. Deleted: {Deleted}, Warned: {Warned}",
                deleted, toWarn.Count);
        }
    }
}
