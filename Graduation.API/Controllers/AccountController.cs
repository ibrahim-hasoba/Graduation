using Auth.DTOs;
using Graduation.API.Errors;
using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Shared.DTOs;
using Shared.DTOs.Auth;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

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
        private readonly IOtpService _otpService;

        public AccountController(
            UserManager<AppUser> userManager,
            JwtHandler jwtHandler,
            IEmailService emailService,
            IConfiguration configuration,
            IRefreshTokenService refreshTokenService,
            DatabaseContext context,
            IGoogleAuthService googleAuthService,
            IOtpService otpService)
        {
            _userManager = userManager;
            _jwtHandler = jwtHandler;
            _emailService = emailService;
            _configuration = configuration;
            _refreshTokenService = refreshTokenService;
            _context = context;
            _googleAuthService = googleAuthService;
            _otpService = otpService;
        }

        #region Registration & Verification

        [HttpPost("register")]
        [EnableRateLimiting("otp")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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

            // Per-email throttle: prevent frequent OTPs for same email
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recent = await _context.EmailOtps
                .AnyAsync(e => e.Email == user.Email && e.CreatedAt >= oneMinuteAgo);

            if (recent)
                return StatusCode(429, new { success = false, message = "OTP recently sent. Try again in a minute." });

            var lastHourCount = await _context.EmailOtps
                .CountAsync(e => e.Email == user.Email && e.CreatedAt >= DateTime.UtcNow.AddHours(-1));

            if (lastHourCount >= 5)
                return StatusCode(429, new { success = false, message = "Too many OTP requests for this email. Try again later." });

            await SendVerificationEmail(user);

            return StatusCode(201, new { success = true, message = "Registration successful! Please verify your email." });
        }

        [HttpGet("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = GetIpAddress();
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId");

            if (string.IsNullOrEmpty(currentUserId))
                throw new UnauthorizedException("User not authenticated");

            var oldToken = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);

            if (oldToken == null || !oldToken.IsActive)
                throw new UnauthorizedException("Invalid session");

            if (oldToken.UserId != currentUserId)
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null)
                // For security, don't reveal if the user exists. Just return "Check your email".
                return Ok(new { success = true, message = "If your email is in our system, you will receive a reset link." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Build the reset URL (pointing to your Frontend Reset Page)
            var baseUrl = _configuration["AppSettings:ClientUrl"];
            var url = $"{baseUrl}?email={user.Email}&token={encodedToken}";

            await _emailService.SendPasswordResetEmailAsync(user.Email!, user.FirstName, url);

            return Ok(new { success = true, message = "Reset link sent to your email." });
        }

        /*
        [HttpGet("reset-password-test")]
        public IActionResult ResetPasswordTest(
                    [FromQuery] string email,
                    [FromQuery] string token)
        {
            return Ok(new
            {
                Email = email,
                Token = token,
                Message = "Reset link opened successfully"
            });
        }
        */
        #region Social & Utility

        [HttpPost("resend-verification-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [EnableRateLimiting("otp")]
        public async Task<IActionResult> ResendVerification([FromBody] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.EmailConfirmed)
                return Ok(new { message = "Verification email sent if applicable." });

            // Per-email throttle (same checks as registration)
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recent = await _context.EmailOtps
                .AnyAsync(e => e.Email == user.Email && e.CreatedAt >= oneMinuteAgo);

            if (recent)
                return StatusCode(429, new { success = false, message = "OTP recently sent. Try again in a minute." });

            var lastHourCount = await _context.EmailOtps
                .CountAsync(e => e.Email == user.Email && e.CreatedAt >= DateTime.UtcNow.AddHours(-1));

            if (lastHourCount >= 5)
                return StatusCode(429, new { success = false, message = "Too many OTP requests for this email. Try again later." });

            // Generate OTP and send
            var code = await _otpService.GenerateOtpAsync(user.Email);
            await _emailService.SendEmailOtpAsync(user.Email, user.FirstName, code);

            return Ok(new { success = true, message = "A new verification code has been sent." });
        }

        #endregion

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);

            return Ok(new { success = true, data = user });
        }

        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
            // Generate OTP and send via email
            var code = await _otpService.GenerateOtpAsync(user.Email!);
            await _emailService.SendEmailOtpAsync(user.Email!, user.FirstName, code);
        }

        [HttpPost("verify-email-otp")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VerifyEmailOtp([FromBody] Auth.DTOs.VerifyEmailOtpDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Code))
                throw new BadRequestException("Email and code are required");

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new NotFoundException("User not found");

            var valid = await _otpService.ValidateOtpAsync(dto.Email, dto.Code);
            if (!valid)
                throw new BadRequestException("Invalid or expired verification code");

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException("Failed to confirm email");

            return Ok(new { success = true, message = "Email verified successfully" });
        }

        #endregion
    }
}