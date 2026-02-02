using Auth.DTOs;
using Graduation.API.Errors;
using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Shared.DTOs;
using Shared.DTOs.Auth;
using System.ComponentModel.DataAnnotations;
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
        private readonly IFacebookAuthService _facebookAuthService;

        public AccountController(
            UserManager<AppUser> userManager,
            JwtHandler jwtHandler,
            IEmailService emailService,
            IConfiguration configuration,
            IFacebookAuthService facebookAuthService, 
            IRefreshTokenService refreshTokenService)
        {
            _userManager = userManager;
            _jwtHandler = jwtHandler;
            _emailService = emailService;
            _configuration = configuration;
            _facebookAuthService = facebookAuthService;
            _refreshTokenService = refreshTokenService;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserForRegisterDto userForRegistration)
        {
            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(userForRegistration.Email!);
            if (existingUser != null)
                throw new ConflictException("A user with this email already exists");

            var user = new AppUser
            {
                FirstName = userForRegistration.FirstName ?? string.Empty,
                LastName = userForRegistration.LastName ?? string.Empty,
                Email = userForRegistration.Email,
                UserName = userForRegistration.Email,
                EmailConfirmed = false // Email not confirmed yet
            };

            var result = await _userManager.CreateAsync(user, userForRegistration.Password!);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException(errors);
            }

            // Add default Customer role
            await _userManager.AddToRoleAsync(user, "Customer");

            // Generate email verification token
            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));

            // Build verification URL
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5069";
            var verificationUrl = $"{baseUrl}/api/account/verify-email?userId={user.Id}&token={encodedToken}";

            // Send verification email
            await _emailService.SendEmailVerificationAsync(user.Email!, user.FirstName, verificationUrl);

            return StatusCode(201, new
            {
                success = true,
                message = "Registration successful! Please check your email to verify your account.",
                data = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    emailVerificationSent = true
                }
            });
        }

        /// <summary>
        /// Verify email address
        /// </summary>
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string userId, [FromQuery] string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                throw new BadRequestException("Invalid verification link");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            if (user.EmailConfirmed)
            {
                return Ok(new
                {
                    success = true,
                    message = "Email already verified. You can login now."
                });
            }

            // Decode token
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

            // Confirm email
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException($"Email verification failed: {errors}");
            }

            return Ok(new
            {
                success = true,
                message = "Email verified successfully! You can now login to your account.",
                data = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    emailVerified = true
                }
            });
        }

        /// <summary>
        /// Resend verification email
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null)
                throw new NotFoundException("User not found");

            if (user.EmailConfirmed)
                throw new BadRequestException("Email is already verified");

            // Generate new token
            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));

            // Build verification URL
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5069";
            var verificationUrl = $"{baseUrl}/api/account/verify-email?userId={user.Id}&token={encodedToken}";

            // Send verification email
            await _emailService.SendEmailVerificationAsync(user.Email!, user.FirstName, verificationUrl);

            return Ok(new
            {
                success = true,
                message = "Verification email sent! Please check your inbox."
            });
        }

        /// <summary>
        /// Login with email and password
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForLoginDto userForAuthentication)
        {
            var user = await _userManager.FindByEmailAsync(userForAuthentication.Email!);

            if (user == null)
                throw new UnauthorizedException("Invalid email or password");

            if (!await _userManager.CheckPasswordAsync(user, userForAuthentication.Password!))
                throw new UnauthorizedException("Invalid email or password");

            // Check if email is verified
            if (!user.EmailConfirmed)
                throw new UnauthorizedException("Please verify your email before logging in. Check your inbox for the verification link.");

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtHandler.CreateToken(user, roles);

            // Generate refresh token
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);

            return Ok(new
            {
                success = true,
                message = "Login successful",
                data = new TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresIn = 3600, // 1 hour in seconds
                    TokenType = "Bearer"
                },
                user = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    roles = roles,
                    emailVerified = user.EmailConfirmed
                }
            });
        }
        /// <summary>
        /// Refresh access token
        /// </summary>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Validate refresh token
            if (!await _refreshTokenService.ValidateRefreshTokenAsync(dto.RefreshToken))
                throw new UnauthorizedException("Invalid or expired refresh token");

            var refreshToken = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (refreshToken == null)
                throw new UnauthorizedException("Invalid refresh token");

            var user = await _userManager.FindByIdAsync(refreshToken.UserId);
            if (user == null)
                throw new NotFoundException("User not found");

            // Generate new tokens
            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _jwtHandler.CreateToken(user, roles);
            var newRefreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);

            // Revoke old refresh token
            await _refreshTokenService.RevokeTokenAsync(dto.RefreshToken, ipAddress, newRefreshToken.Token);

            return Ok(new
            {
                success = true,
                message = "Token refreshed successfully",
                data = new TokenResponseDto
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken.Token,
                    ExpiresIn = 3600,
                    TokenType = "Bearer"
                }
            });
        }
        /// <summary>
        /// Revoke refresh token (logout)
        /// </summary>
        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            try
            {
                await _refreshTokenService.RevokeTokenAsync(dto.RefreshToken, ipAddress);
                return Ok(new { success = true, message = "Token revoked successfully" });
            }
            catch (BadRequestException)
            {
                throw new BadRequestException("Invalid token");
            }
        }
        /// <summary>
        /// Login with Facebook
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("facebook-login")]
        public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginDto dto)
        {
            // Validate Facebook access token
            var isValid = await _facebookAuthService.ValidateAccessTokenAsync(dto.AccessToken);
            if (!isValid)
                throw new UnauthorizedException("Invalid Facebook access token");

            // Get user info from Facebook
            var facebookUser = await _facebookAuthService.GetUserInfoAsync(dto.AccessToken);
            if (facebookUser == null || string.IsNullOrEmpty(facebookUser.Email))
                throw new BadRequestException("Unable to get user information from Facebook");

            // Check if user exists
            var user = await _userManager.FindByEmailAsync(facebookUser.Email);

            if (user == null)
            {
                // Create new user from Facebook data
                var names = facebookUser.Name.Split(' ', 2);
                var firstName = names.Length > 0 ? names[0] : facebookUser.Name;
                var lastName = names.Length > 1 ? names[1] : "";

                user = new AppUser
                {
                    UserName = facebookUser.Email,
                    Email = facebookUser.Email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true, // Facebook emails are verified
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new BadRequestException($"Failed to create user: {errors}");
                }

                // Add Customer role
                await _userManager.AddToRoleAsync(user, "Customer");
            }

            // Generate JWT token and refresh token
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtHandler.CreateToken(user, roles);

            // Generate refresh token (THIS WAS MISSING)
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);

            return Ok(new
            {
                success = true,
                message = "Facebook login successful",
                data = new TokenResponseDto  // Changed to use TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresIn = 3600,
                    TokenType = "Bearer"
                },
                user = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    roles = roles,
                    isNewUser = user.CreatedAt > DateTime.UtcNow.AddMinutes(-1)
                }
            });
        }

        /// <summary>
        /// Get current user profile
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    emailVerified = user.EmailConfirmed,
                    roles = roles
                }
            });
        }
        // ADD THESE METHODS TO YOUR EXISTING AccountController.cs

        /// <summary>
        /// Request password reset email
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // Don't reveal if user exists or not (security best practice)
            if (user == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "If an account exists with that email, a password reset link has been sent."
                });
            }

            // Generate password reset token
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));

            // Build reset URL
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5069";
            var resetUrl = $"{baseUrl}/api/account/reset-password?email={user.Email}&token={encodedToken}";

            // Send reset email
            await _emailService.SendPasswordResetEmailAsync(user.Email!, user.FirstName, resetUrl);

            return Ok(new
            {
                success = true,
                message = "If an account exists with that email, a password reset link has been sent."
            });
        }

        /// <summary>
        /// Reset password with token
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new NotFoundException("User not found");

            // Decode token
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token));

            // Reset password
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException($"Password reset failed: {errors}");
            }

            return Ok(new
            {
                success = true,
                message = "Password reset successfully. You can now login with your new password."
            });
        }

        /// <summary>
        /// Change password (authenticated users)
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException($"Password change failed: {errors}");
            }

            return Ok(new
            {
                success = true,
                message = "Password changed successfully"
            });
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.PhoneNumber = dto.PhoneNumber;
            user.ProfilePictureUrl = dto.ProfilePictureUrl;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException($"Profile update failed: {errors}");
            }

            return Ok(new
            {
                success = true,
                message = "Profile updated successfully",
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    phoneNumber = user.PhoneNumber,
                    profilePictureUrl = user.ProfilePictureUrl
                }
            });
        }

        // DTO for UpdateProfile
        public class UpdateProfileDto
        {
            [Required]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            public string LastName { get; set; } = string.Empty;

            [Phone]
            public string? PhoneNumber { get; set; }

            public string? ProfilePictureUrl { get; set; }
        }
    }

    // New DTO for resend verification
    public class ResendVerificationDto
    {
        public string? Email { get; set; }
    }
}