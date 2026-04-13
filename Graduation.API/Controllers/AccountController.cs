using Auth.DTOs;
using Graduation.API.Extensions;
using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.DTOs.Auth;
using Shared.Errors;
using System.Security.Claims;

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
        private readonly IImageService _imageService;
        private readonly ICodeAssignmentService _codeAssignment;
        private readonly IOrderService _orderService;
        private readonly ILanguageService _lang;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AccountController> _logger; 

        public AccountController(
            UserManager<AppUser> userManager,
            JwtHandler jwtHandler,
            IEmailService emailService,
            IConfiguration configuration,
            IRefreshTokenService refreshTokenService,
            DatabaseContext context,
            IGoogleAuthService googleAuthService,
            IOtpService otpService,
            IImageService imageService,
            ICodeAssignmentService codeAssignment,
            IOrderService orderService,
            ILanguageService lang,
            INotificationService notificationService,
            ILogger<AccountController> logger) 
        {
            _userManager = userManager;
            _jwtHandler = jwtHandler;
            _emailService = emailService;
            _configuration = configuration;
            _refreshTokenService = refreshTokenService;
            _context = context;
            _googleAuthService = googleAuthService;
            _otpService = otpService;
            _imageService = imageService;
            _codeAssignment = codeAssignment;
            _orderService = orderService;
            _lang = lang;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost("register")]
        [EnableRateLimiting("otp")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] UserForRegisterDto userDto)
        {
            var existingByEmail = await _userManager.FindByEmailAsync(userDto.Email!);
            if (existingByEmail != null)
                throw new ConflictException(_lang.GetMessage("Email_AlreadyExists"));

            if (!string.IsNullOrWhiteSpace(userDto.PhoneNumber))
            {
                var existingByPhone = await _userManager.Users
                    .AnyAsync(u => u.PhoneNumber == userDto.PhoneNumber);
                if (existingByPhone)
                    throw new ConflictException(_lang.GetMessage("Phone_AlreadyRegistered"));
            }

            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recent = await _context.EmailOtps
                .AnyAsync(e => e.Email == userDto.Email && e.CreatedAt >= oneMinuteAgo);
            if (recent)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("OTP_RecentlySent")));

            var lastHourCount = await _context.EmailOtps
                .CountAsync(e => e.Email == userDto.Email && e.CreatedAt >= DateTime.UtcNow.AddHours(-1));
            if (lastHourCount >= 5)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("OTP_TooMany")));

            var user = new AppUser
            {
                FirstName = userDto.FirstName ?? string.Empty,
                LastName = userDto.LastName ?? string.Empty,
                Email = userDto.Email,
                UserName = userDto.Email,
                EmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                PhoneNumber = userDto.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, userDto.Password!);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            await _codeAssignment.AssignUserCodeAsync(user);

            try
            {
                await SendVerificationEmail(user);
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);
                throw new BadRequestException(_lang.GetMessage("Registration_EmailFailed"));
            }

            return StatusCode(201, new ApiResult(message: _lang.GetMessage("Registration_Success")));
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Login([FromBody] UserForLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email!);
            if (user == null)
                throw new NotFoundException(_lang.GetMessage("Account_NotFound"));

            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException(_lang.GetMessage("Account_Locked"));

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password!))
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedException(_lang.GetMessage("Invalid_Credentials"));
            }

            if (!user.EmailConfirmed)
                throw new UnauthorizedException(_lang.GetMessage("Email_NotVerified"));

            if (string.IsNullOrEmpty(user.Code))
                await _codeAssignment.AssignUserCodeAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);

            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        user.Id,
                        "New Login Detected",
                        $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                        "If this wasn't you, please change your password immediately.",
                        "Security");
                }
                catch { }
            });

            return await GenerateAuthResponse(user, loginDto.RememberMe);
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
            try
            {
                var payload = await _googleAuthService.VerifyGoogleTokenAsync(dto.IdToken);
                if (payload == null)
                    return Unauthorized(new { message = _lang.GetMessage("Login_GoogleInvalidToken") });

                var user = await _userManager.FindByEmailAsync(payload.Email);

                if (user == null)
                {
                    user = new AppUser
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        FirstName = payload.GivenName ?? "GoogleUser",
                        LastName = payload.FamilyName ?? string.Empty,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        return BadRequest(new { message = _lang.GetMessage("Login_GoogleFailed"), details = errors });
                    }

                    await _userManager.AddToRoleAsync(user, "Customer");
                    await _codeAssignment.AssignUserCodeAsync(user);
                }
                else
                {
                    
                    if (!user.EmailConfirmed)
                    {
                        user.EmailConfirmed = true;
                        await _userManager.UpdateAsync(user);
                    }

                    if (string.IsNullOrEmpty(user.Code))
                        await _codeAssignment.AssignUserCodeAsync(user);
                }

                return await GenerateAuthResponse(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google login failed");
                return StatusCode(500, new ApiResult(message: "Server error"));
            }
        }

        [HttpPost("update-fcm-token")]
        [Authorize]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FcmToken))
                return BadRequest(new ApiResult(message: _lang.GetMessage("FCM_Empty")));

            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResult(message: _lang.GetMessage("NotAuthenticated")));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new ApiResult(message: _lang.GetMessage("User_NotFound")));

            var isNewToken = user.FcmToken != request.FcmToken;

            user.FcmToken = request.FcmToken;
            await _userManager.UpdateAsync(user);

            if (isNewToken)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(
                            userId,
                            "New Login Detected",
                            $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                            "If this wasn't you, please change your password immediately.",
                            "Security");
                    }
                    catch { }
                });
            }

            return Ok(new ApiResult(message: _lang.GetMessage("FCM_Updated")));
        }

        [HttpPost("refresh-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = GetIpAddress();
            var oldToken = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (oldToken == null || !oldToken.IsActive)
                throw new UnauthorizedException(_lang.GetMessage("Session_Invalid"));

            var user = await _userManager.FindByIdAsync(oldToken.UserId);
            if (user == null)
                throw new UnauthorizedException(_lang.GetMessage("User_NoLongerExists"));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtHandler.CreateToken(user, roles);
                var newToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);
                await _refreshTokenService.RevokeTokenAsync(oldToken.Token, ipAddress, newToken.Token);
                await transaction.CommitAsync();
                return Ok(new ApiResult(data: CreateTokenResponse(accessToken, newToken.Token)));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw new BadRequestException(_lang.GetMessage("Token_RefreshFailed"));
            }
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null)
                return Ok(new ApiResult(message: _lang.GetMessage("ForgotPassword_Sent")));

            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recent = await _context.EmailOtps
                .AnyAsync(e => e.Email == dto.Email && e.Purpose == "password_reset" && e.CreatedAt >= oneMinuteAgo);
            if (recent)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("Code_RecentlySent")));

            var lastHourCount = await _context.EmailOtps
                .CountAsync(e => e.Email == dto.Email && e.Purpose == "password_reset"
                              && e.CreatedAt >= DateTime.UtcNow.AddHours(-1));
            if (lastHourCount >= 5)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("Code_TooMany")));

            var code = await _otpService.GenerateOtpAsync(dto.Email!, purpose: "password_reset");
            await _emailService.SendEmailOtpAsync(user.Email!, user.FirstName, code);
            return Ok(new ApiResult(message: _lang.GetMessage("ForgotPassword_CodeSent")));
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithOtpDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new BadRequestException(_lang.GetMessage("Password_Invalid"));

            var valid = await _otpService.ValidateOtpAsync(dto.Email, dto.Code, purpose: "password_reset");
            if (!valid)
                throw new BadRequestException(_lang.GetMessage("Password_CodeInvalid"));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            return Ok(new ApiResult(message: _lang.GetMessage("Password_ResetSuccess")));
        }

        [HttpPost("resend-verification-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [EnableRateLimiting("otp")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null || user.EmailConfirmed)
                return Ok(new ApiResult(message: _lang.GetMessage("Verification_Sent")));

            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recent = await _context.EmailOtps
                .AnyAsync(e => e.Email == user.Email && e.CreatedAt >= oneMinuteAgo);
            if (recent)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("OTP_RecentlySent")));

            var lastHourCount = await _context.EmailOtps
                .CountAsync(e => e.Email == user.Email && e.CreatedAt >= DateTime.UtcNow.AddHours(-1));
            if (lastHourCount >= 5)
                return StatusCode(429, new ApiResponse(429, _lang.GetMessage("OTP_TooMany")));

            await SendVerificationEmail(user);
            return Ok(new ApiResult(message: _lang.GetMessage("Verification_NewSent")));
        }

        [HttpPost("revoke-token")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var token = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);

            if (token == null || token.UserId != userId)
                throw new UnauthorizedException(_lang.GetMessage("Invalid_Credentials"));

            await _refreshTokenService.RevokeTokenAsync(dto.RefreshToken, GetIpAddress());
            return Ok(new ApiResult(message: _lang.GetMessage("Logout_Success")));
        }

        [HttpPost("admin/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AdminLogin([FromBody] UserForLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email!);
            if (user == null)
                throw new NotFoundException(_lang.GetMessage("Account_NotFound"));

            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException(_lang.GetMessage("Account_Locked"));

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password!))
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedException(_lang.GetMessage("Invalid_Credentials"));
            }

            if (!user.EmailConfirmed)
                throw new UnauthorizedException(_lang.GetMessage("Email_NotVerified"));

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
                throw new UnauthorizedException(_lang.GetMessage("NotAdmin"));

            if (string.IsNullOrEmpty(user.Code))
                await _codeAssignment.AssignUserCodeAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        user.Id,
                        "New Login Detected",
                        $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                        "If this wasn't you, please change your password immediately.",
                        "Security");
                }
                catch { }
            });

            return await GenerateAuthResponse(user, loginDto.RememberMe);
        }

        [HttpGet("profile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            return Ok(new ApiResult(data: new
            {
                userCode = user.Code,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                profilePictureUrl = _imageService.GetFullImageUrl(user.ProfilePictureUrl!),
                profilePictureRelativePath = user.ProfilePictureUrl
            }));
        }

        [HttpPut("update-profile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.PhoneNumber = dto.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException($"Failed to update profile: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            return Ok(new ApiResult(
                data: new { user.FirstName, user.LastName, user.PhoneNumber, user.Email },
                message: _lang.GetMessage("Profile_UpdateSuccess")));
        }

        [HttpPost("upload-profile-picture")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);

            var imageUrl = await _imageService.UploadImageAsync(file, "profiles");
            user.ProfilePictureUrl = imageUrl;
            await _userManager.UpdateAsync(user);

            return Ok(new ApiResult(data: new
            {
                relativePath = imageUrl,
                fullUrl = _imageService.GetFullImageUrl(imageUrl)
            }, message: _lang.GetMessage("ProfilePicture_UpdateSuccess")));
        }

        [HttpDelete("delete-profile-picture")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);
                user.ProfilePictureUrl = null;
                await _userManager.UpdateAsync(user);
            }

            return Ok(new ApiResult(message: _lang.GetMessage("ProfilePicture_DeleteSuccess")));
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
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                throw new BadRequestException(_lang.GetMessage("Password_Invalid"));

            await _refreshTokenService.RevokeUserTokensAsync(userId!, GetIpAddress());
            return Ok(new ApiResult(message: _lang.GetMessage("Password_ChangeSuccess")));
        }

        [HttpPost("verify-email-otp")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
                throw new BadRequestException("Email and code are required");

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            var isValid = await _otpService.ValidateOtpAsync(dto.Email, dto.Code);
            if (!isValid)
                throw new BadRequestException(_lang.GetMessage("Password_CodeInvalid"));

            if (user.EmailConfirmed)
                return Ok(new ApiResult(message: _lang.GetMessage("Email_AlreadyVerified")));

            user.EmailConfirmed = true;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new BadRequestException(_lang.GetMessage("Email_ConfirmFailed"));

            return await GenerateAuthResponse(user);
        }

        [HttpPost("verify-reset-code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
        {
            var isValid = await _otpService.PeekOtpAsync(dto.Email, dto.Code, purpose: "password_reset");
            if (!isValid)
                throw new BadRequestException(_lang.GetMessage("Password_CodeInvalid"));

            return Ok(new ApiResult(message: _lang.GetMessage("ResetCode_Valid")));
        }

        [HttpDelete("delete-account")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) throw new NotFoundException(_lang.GetMessage("User_NotFound"));

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new UnauthorizedException(_lang.GetMessage("Invalid_Credentials"));

            await _refreshTokenService.RevokeUserTokensAsync(userId!, GetIpAddress());

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);

            var userEmail = user.Email!;
            await CleanupUserDataAsync(userId!, userEmail);

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            return Ok(new ApiResult(message: _lang.GetMessage("Account_Deleted")));
        }


        private async Task CleanupUserDataAsync(string userId, string userEmail)
        {
            await _orderService.HandleUserAccountDeletionAsync(userId);

            await _context.CartItems
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Wishlists
                .Where(w => w.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Notifications
                .Where(n => n.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.EmailOtps
                .Where(o => o.Email == userEmail)
                .ExecuteDeleteAsync();
        }

        private async Task<IActionResult> GenerateAuthResponse(AppUser user, bool rememberMe = false)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtHandler.CreateToken(user, roles);
            var refreshToken = await _refreshTokenService
                .GenerateRefreshTokenAsync(user.Id, GetIpAddress(), rememberMe);

            var hasAddress = await _context.UserAddresses.AnyAsync(a => a.UserId == user.Id);

            var response = new AuthResponseDto
            {
                Token = new TokenResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresIn = 3600,
                    TokenType = "Bearer"
                },
                User = new UserInfoDto
                {
                    UserCode = user.Code ?? string.Empty,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles,
                    HasAddress = hasAddress,
                    ProfilePictureUrl = user.ProfilePictureUrl!
                }
            };

            return Ok(new ApiResult(data: response));
        }

        private TokenResponseDto CreateTokenResponse(string access, string refresh) => new()
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        private string GetIpAddress() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private async Task SendVerificationEmail(AppUser user)
        {
            var code = await _otpService.GenerateOtpAsync(user.Email!);
            await _emailService.SendEmailOtpAsync(user.Email!, user.FirstName, code);
        }
    }
}
