using Auth.DTOs;
using Graduation.API.Errors;
using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Shared.DTOs;
using Shared.DTOs.Auth;
using System.Security.Claims;
using System.Text;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtHandler _jwtHandler;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly DatabaseContext _context;
        private readonly IGoogleAuthService _googleAuthService;

        public AccountController(
            UserManager<AppUser> userManager,
            JwtHandler jwtHandler,
            IEmailService emailService,
            IConfiguration configuration,
            IRefreshTokenService refreshTokenService,
            DatabaseContext context,
            IGoogleAuthService googleAuthService)
        {
            _userManager = userManager;
            _jwtHandler = jwtHandler;
            _emailService = emailService;
            _configuration = configuration;
            _refreshTokenService = refreshTokenService;
            _context = context;
            _googleAuthService = googleAuthService;
        }

        #region Registration & Verification

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserForRegisterDto userDto)
        {
            var existingUser = await _userManager.FindByEmailAsync(userDto.Email!);
            if (existingUser != null)
                throw new ConflictException("A user with this email already exists");

            var user = new AppUser
            {
                FirstName = userDto.FirstName ?? string.Empty,
                LastName = userDto.LastName ?? string.Empty,
                Email = userDto.Email,
                UserName = userDto.Email,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, userDto.Password!);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            await SendVerificationEmail(user);

            return StatusCode(201, new { success = true, message = "Registration successful! Please verify your email." });
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string userId, [FromQuery] string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException("User not found");

            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
                throw new BadRequestException("Email verification failed.");

            return Ok(new { success = true, message = "Email verified successfully!" });
        }

        #endregion

        #region Login & Token Management

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email!);
            if (user == null) throw new UnauthorizedException("Invalid credentials");

            // Check for Account Lockout (Brute Force Protection)
            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException("Account locked. Please try again later.");

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password!))
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedException("Invalid credentials");
            }

            if (!user.EmailConfirmed)
                throw new UnauthorizedException("Please verify your email first.");

            await _userManager.ResetAccessFailedCountAsync(user);

            return await GenerateAuthResponse(user);
        }
        public class GoogleLoginDto
        {
            public string IdToken { get; set; } = string.Empty;
        }
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            // 1. Verify token with Google
            var payload = await _googleAuthService.VerifyGoogleTokenAsync(dto.IdToken);
            if (payload == null)
                throw new UnauthorizedException("Invalid Google token.");

            // 2. Check if user exists in our DB
            var user = await _userManager.FindByEmailAsync(payload.Email);

            if (user == null)
            {
                // 3. Register user if they don't exist
                user = new AppUser
                {
                    Email = payload.Email,
                    UserName = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    EmailConfirmed = true, // Google already verified this email
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                    throw new BadRequestException("Failed to create user from Google account.");

                await _userManager.AddToRoleAsync(user, "Customer");
            }

            // 4. Generate our local JWT and Refresh Token
            return await GenerateAuthResponse(user);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = GetIpAddress();
            var oldToken = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);

            if (oldToken == null || !oldToken.IsActive)
                throw new UnauthorizedException("Invalid session");

            var user = await _userManager.FindByIdAsync(oldToken.UserId);
            if (user == null) throw new UnauthorizedException("User no longer exists");

            // Use Transaction to ensure rotation is Atomic
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtHandler.CreateToken(user, roles);
                var newToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);

                await _refreshTokenService.RevokeTokenAsync(oldToken.Token, ipAddress, newToken.Token);

                await transaction.CommitAsync();

                return Ok(new { success = true, data = CreateTokenResponse(accessToken, newToken.Token) });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw new BadRequestException("Token refresh failed");
            }
        }
        #region Password Recovery

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null)
                // For security, don't reveal if the user exists. Just return "Check your email".
                return Ok(new { success = true, message = "If your email is in our system, you will receive a reset link." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Build the reset URL (pointing to your Frontend Reset Page)
            var baseUrl = _configuration["AppSettings:ClientUrl"]; // e.g., http://localhost:4200/reset-password
            var url = $"{baseUrl}?email={user.Email}&token={encodedToken}";

            await _emailService.SendPasswordResetEmailAsync(user.Email!, user.FirstName, url);

            return Ok(new { success = true, message = "Reset link sent to your email." });
        }
        #region Social & Utility

        

        [HttpPost("resend-verification-email")]
        public async Task<IActionResult> ResendVerification([FromBody] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.EmailConfirmed)
                return Ok(new { message = "Verification email sent if applicable." });

            await SendVerificationEmail(user);
            return Ok(new { success = true, message = "A new verification link has been sent." });
        }

        #endregion

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null) throw new BadRequestException("Invalid request");

            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token!));
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword!);

            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            return Ok(new { success = true, message = "Password has been reset successfully." });
        }

        #endregion

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var token = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);

            if (token == null || token.UserId != userId)
                throw new UnauthorizedException("Unauthorized token revocation");

            await _refreshTokenService.RevokeTokenAsync(dto.RefreshToken, GetIpAddress());
            return Ok(new { success = true, message = "Logged out successfully" });
        }

        #endregion

        #region Profile & Security

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);

            return Ok(new { success = true, data = user });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);

            var result = await _userManager.ChangePasswordAsync(user!, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                throw new BadRequestException("Failed to change password.");

            // Force logout from all other devices for security
            await _refreshTokenService.RevokeUserTokensAsync(userId!, GetIpAddress());

            return Ok(new { success = true, message = "Password updated. Other sessions revoked." });
        }

        #endregion

        #region Helpers

        private async Task<IActionResult> GenerateAuthResponse(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtHandler.CreateToken(user, roles);
            var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, GetIpAddress());

            return Ok(new
            {
                success = true,
                data = CreateTokenResponse(accessToken, refreshToken.Token),
                user = new { user.Email, user.FirstName, user.LastName, roles }
            });
        }

        private TokenResponseDto CreateTokenResponse(string access, string refresh) => new()
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private async Task SendVerificationEmail(AppUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5069";
            var url = $"{baseUrl}/api/account/verify-email?userId={user.Id}&token={encodedToken}";

            await _emailService.SendEmailVerificationAsync(user.Email!, user.FirstName, url);
        }

        #endregion
    }
}