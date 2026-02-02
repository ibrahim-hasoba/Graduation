using Graduation.DAL.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Graduation.BLL.JwtFeatures
{
    public class JwtHandler
    {
        private readonly IConfiguration _configuration;
        private readonly IConfigurationSection _jwtSetting;

        public JwtHandler(IConfiguration configuration)
        {
            _configuration = configuration;
            _jwtSetting = _configuration.GetSection("JWTSettings");
        }

        public string CreateToken(AppUser user, IList<string> roles)
        {
            var signingCredentials = GetSigningCredentials();
            var claims = GetClaims(user, roles);
            var tokenOptions = GenerateTokenOptions(signingCredentials, claims);
            var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);
            return token;
        }

        private SigningCredentials GetSigningCredentials()
        {
            var key = Encoding.UTF8.GetBytes(_jwtSetting["securityKey"]!);
            var secret = new SymmetricSecurityKey(key);
            return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
        }

        private List<Claim> GetClaims(AppUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("userId", user.Id)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }

        // REPLACE the GenerateTokenOptions method with this:
        private JwtSecurityToken GenerateTokenOptions(SigningCredentials signingCredentials, List<Claim> claims)
        {
            // Changed from 5 minutes to 1 hour for better UX
            var expiryMinutes = _jwtSetting["expiryInMinutes"];
            var expiry = string.IsNullOrEmpty(expiryMinutes)
                ? DateTime.UtcNow.AddHours(1)  // Changed from 60 minutes to 1 hour
                : DateTime.UtcNow.AddMinutes(Convert.ToDouble(expiryMinutes));

            var tokenOptions = new JwtSecurityToken(
                issuer: _jwtSetting["validIssuer"],
                audience: _jwtSetting["validAudience"],
                claims: claims,
                expires: expiry,
                signingCredentials: signingCredentials
            );

            return tokenOptions;
        }
    }
}