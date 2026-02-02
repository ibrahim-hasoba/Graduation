using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Graduation.BLL.Services.Implementations
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(DatabaseContext context, ILogger<RefreshTokenService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string ipAddress)
        {
            var token = new RefreshToken
            {
                UserId = userId,
                Token = GenerateTokenString(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token generated for user {UserId} from IP {IpAddress}",
                userId, ipAddress);

            return token;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        // CRITICAL FIX: Validate token belongs to the authenticated user
        public async Task<bool> ValidateRefreshTokenAsync(string token, string? userId = null)
        {
            var refreshToken = await GetRefreshTokenAsync(token);

            if (refreshToken == null || !refreshToken.IsActive)
                return false;

            // SECURITY: If userId is provided, verify token belongs to that user
            if (!string.IsNullOrEmpty(userId) && refreshToken.UserId != userId)
            {
                _logger.LogWarning("Refresh token validation failed: Token belongs to different user. " +
                    "Expected: {ExpectedUserId}, Actual: {ActualUserId}", userId, refreshToken.UserId);
                return false;
            }

            return true;
        }

        public async Task RevokeTokenAsync(string token, string ipAddress, string? replacedByToken = null)
        {
            var refreshToken = await GetRefreshTokenAsync(token);

            if (refreshToken == null || !refreshToken.IsActive)
                throw new BadRequestException("Invalid token");

            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = replacedByToken;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token revoked for user {UserId} from IP {IpAddress}",
                refreshToken.UserId, ipAddress);
        }

        public async Task RevokeAllUserTokensAsync(string userId, string ipAddress)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("All refresh tokens revoked for user {UserId}. Total: {Count}",
                userId, tokens.Count);
        }

        public async Task RemoveExpiredTokensAsync()
        {
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed {Count} expired refresh tokens", expiredTokens.Count);
        }

        // NEW: Get all active tokens for a user (for security dashboard)
        public async Task<List<RefreshToken>> GetUserActiveTokensAsync(string userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive)
                .OrderByDescending(rt => rt.CreatedAt)
                .ToListAsync();
        }

        // NEW: Revoke specific token by ID (for user to manage their sessions)
        public async Task RevokeTokenByIdAsync(int tokenId, string userId, string ipAddress)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Id == tokenId && rt.UserId == userId);

            if (token == null)
                throw new NotFoundException("Refresh token not found");

            if (!token.IsActive)
                throw new BadRequestException("Token is already revoked");

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;

            await _context.SaveChangesAsync();
        }

        private static string GenerateTokenString()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}