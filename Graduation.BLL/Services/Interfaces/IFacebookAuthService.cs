using Shared.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IFacebookAuthService
    {
        Task<FacebookUserDataDto?> GetUserInfoAsync(string accessToken);
        Task<bool> ValidateAccessTokenAsync(string accessToken);
    }
}
