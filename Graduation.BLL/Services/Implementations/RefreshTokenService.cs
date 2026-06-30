using Graduation.BLL.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Graduation.BLL.Services.Implementations
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IUnitOfWork _uow;

        public RefreshTokenService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string ipAddress , bool rememberMe = false)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = GenerateToken(),
                ExpiresAt = rememberMe
                        ? DateTime.UtcNow.AddDays(30)
                        : DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _uow.Repository<RefreshToken>().Add(refreshToken);
            await _uow.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _uow.Repository<RefreshToken>().Query()
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, string userId)
        {
            var refreshToken = await _uow.Repository<RefreshToken>().Query()
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null || refreshToken.UserId != userId)
                return null;

            if (!refreshToken.IsActive)
                return null;

            return refreshToken;
        }

        public async Task RevokeTokenAsync(string token, string ipAddress, string? replacedByToken = null)
        {
            var refreshToken = await _uow.Repository<RefreshToken>().Query()
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null)
                throw new BadRequestException("Invalid token");

            if (!refreshToken.IsActive)
                throw new BadRequestException("Token is already revoked or expired");

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = replacedByToken;

            await _uow.SaveChangesAsync();
        }

        public async Task RevokeUserTokensAsync(string userId, string ipAddress)
        {
            var userTokens = await _uow.Repository<RefreshToken>().Query()
                    .Where(rt => rt.UserId == userId
                                    && rt.RevokedAt == null
                                    && rt.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

            foreach (var token in userTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
            }

            await _uow.SaveChangesAsync();
        }

        public async Task CleanupExpiredTokensAsync()
        {
            var expiredTokens = await _uow.Repository<RefreshToken>().Query()
                .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _uow.Repository<RefreshToken>().DeleteRange(expiredTokens);
            await _uow.SaveChangesAsync();
        }

        private static string GenerateToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
