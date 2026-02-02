using Graduation.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string ipAddress);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task<bool> ValidateRefreshTokenAsync(string token, string? userId = null);
        Task RevokeTokenAsync(string token, string ipAddress, string? replacedByToken = null);
        Task RevokeAllUserTokensAsync(string userId, string ipAddress);
        Task RemoveExpiredTokensAsync();
    }
}
