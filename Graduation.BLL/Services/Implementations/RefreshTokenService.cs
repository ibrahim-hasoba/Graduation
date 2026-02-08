using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Graduation.BLL.Services.Implementations
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly DatabaseContext _context;

        public RefreshTokenService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string ipAddress)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = GenerateToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(7), // 7 days expiration
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        // SECURITY FIX: This method now properly validates userId
        public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, string userId)
        {
            var refreshToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            // CRITICAL: Validate that the token belongs to the specified user
            if (refreshToken == null || refreshToken.UserId != userId)
                return null;

            // Check if token is active
            if (!refreshToken.IsActive)
                return null;

            return refreshToken;
        }

        public async Task RevokeTokenAsync(string token, string ipAddress, string? replacedByToken = null)
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
                throw new BadRequestException("Invalid token");

            if (!refreshToken.IsActive)
                throw new BadRequestException("Token is already revoked or expired");

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = replacedByToken;

            await _context.SaveChangesAsync();
        }

        public async Task RevokeUserTokensAsync(string userId, string ipAddress)
        {
            var userTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId
                                    && rt.RevokedAt == null
                                    && rt.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

            foreach (var token in userTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
            }

            await _context.SaveChangesAsync();
        }

        public async Task CleanupExpiredTokensAsync()
        {
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }

        private string GenerateToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
