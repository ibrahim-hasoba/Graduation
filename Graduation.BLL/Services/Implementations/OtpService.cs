using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Graduation.BLL.Services.Implementations
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private const string OtpPrefix = "Otp:";
        private const string RatePrefix = "OtpRate:";

        public OtpService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<string> GenerateOtpAsync(string email, string purpose = "email_verification", int ttlMinutes = 10)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var key = $"{OtpPrefix}{purpose}:{email}";

            var entry = new OtpEntry
            {
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes)
            };

            _cache.Set(key, entry, entry.ExpiresAt);

            RecordSend(email, purpose);

            return Task.FromResult(code);
        }

        public Task<(bool allowed, string? reasonKey)> CheckRateLimitAsync(string email, string purpose = "email_verification")
        {
            var rateKey = $"{RatePrefix}{purpose}:{email}";
            var now = DateTime.UtcNow;

            if (!_cache.TryGetValue(rateKey, out List<DateTime>? sends) || sends is null)
                return Task.FromResult((true, (string?)null));

            sends.RemoveAll(t => now - t > TimeSpan.FromHours(1));

            var oneMinuteAgo = now.AddMinutes(-1);
            if (sends.Any(t => t >= oneMinuteAgo))
                return Task.FromResult<(bool, string?)>((false, purpose == "password_reset" ? "Code_RecentlySent" : "OTP_RecentlySent"));

            if (sends.Count >= 5)
                return Task.FromResult<(bool, string?)>((false, purpose == "password_reset" ? "Code_TooMany" : "OTP_TooMany"));

            return Task.FromResult((true, (string?)null));
        }

        private void RecordSend(string email, string purpose)
        {
            var rateKey = $"{RatePrefix}{purpose}:{email}";
            var now = DateTime.UtcNow;

            var sends = _cache.GetOrCreate(rateKey, _ => new List<DateTime>())!;
            sends.RemoveAll(t => now - t > TimeSpan.FromHours(1));
            sends.Add(now);

            _cache.Set(rateKey, sends, TimeSpan.FromHours(1));
        }

        public Task<bool> PeekOtpAsync(string email, string code, string purpose = "password_reset")
        {
            var key = $"{OtpPrefix}{purpose}:{email}";

            if (!_cache.TryGetValue(key, out OtpEntry? entry) || entry is null)
                return Task.FromResult(false);

            if (entry.ExpiresAt < DateTime.UtcNow)
                return Task.FromResult(false);

            return Task.FromResult(CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(entry.Code),
                System.Text.Encoding.UTF8.GetBytes(code)));
        }

        public Task<bool> ValidateOtpAsync(string email, string code, string purpose = "email_verification")
        {
            var key = $"{OtpPrefix}{purpose}:{email}";

            if (!_cache.TryGetValue(key, out OtpEntry? entry) || entry is null)
                return Task.FromResult(false);

            if (entry.ExpiresAt < DateTime.UtcNow)
                return Task.FromResult(false);

            if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(entry.Code),
                    System.Text.Encoding.UTF8.GetBytes(code)))
                return Task.FromResult(false);

            _cache.Remove(key);
            return Task.FromResult(true);
        }

        private class OtpEntry
        {
            public string Code { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }
    }
}
