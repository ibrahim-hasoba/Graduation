using Google.Apis.Auth;
using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Graduation.BLL.Services.Implementations
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IConfiguration _configuration;

        public GoogleAuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string> { _configuration["GoogleSettings:ClientId"]! }
                };

                return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            }
            catch
            {
                return null; // Invalid token
            }
        }
    }
}