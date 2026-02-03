using Graduation.DAL.Entities;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IRefreshTokenService
    {
        /// <summary>
        /// Generate a new refresh token for a user
        /// </summary>
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string ipAddress);

        /// <summary>
        /// Get refresh token by token string
        /// </summary>
        Task<RefreshToken?> GetRefreshTokenAsync(string token);

        /// <summary>
        /// Validate refresh token and ensure it belongs to the specified user
        /// SECURITY: This method validates both token existence and user ownership
        /// </summary>
        Task<RefreshToken?> ValidateRefreshTokenAsync(string token, string userId);

        /// <summary>
        /// Revoke a specific refresh token
        /// </summary>
        Task RevokeTokenAsync(string token, string ipAddress, string? replacedByToken = null);

        /// <summary>
        /// Revoke all active refresh tokens for a user
        /// </summary>
        Task RevokeUserTokensAsync(string userId, string ipAddress);

        /// <summary>
        /// Clean up expired tokens from the database
        /// </summary>
        Task CleanupExpiredTokensAsync();
    }
}
