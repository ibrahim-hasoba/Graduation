using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Graduation.BLL.Services.Implementations
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private const string OtpPrefix = "Otp:";

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

            return Task.FromResult(code);
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
