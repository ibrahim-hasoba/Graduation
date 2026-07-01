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
using Graduation.BLL.DTOs;
using Graduation.BLL.DTOs.Auth;
using Hangfire;
using Graduation.BLL.Errors;
using Graduation.API.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : BaseController
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
        private readonly IVendorService _vendorService;
        private readonly IBackgroundJobClient _backgroundJobs;
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
            IVendorService vendorService,
            ILanguageService lang,
            IBackgroundJobClient backgroundJobs,
            ILogger<AccountController> logger)
            : base(lang)
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
            _vendorService = vendorService;
            _backgroundJobs = backgroundJobs;
            _logger = logger;
        }
        /// <summary>Registers a new user account and sends an OTP verification email.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("register")]
        [EnableRateLimiting("otp")]
        public async Task<IActionResult> Register([FromBody] UserForRegisterDto userDto)
        {
            var existingByEmail = await _userManager.FindByEmailAsync(userDto.Email!);
            if (existingByEmail != null)
                throw new ConflictException(Lang.GetMessage(LangKeys.Auth.EmailAlreadyExists));

            if (!string.IsNullOrWhiteSpace(userDto.PhoneNumber))
            {
                var existingByPhone = await _userManager.Users
                    .AnyAsync(u => u.PhoneNumber == userDto.PhoneNumber);
                if (existingByPhone)
                    throw new ConflictException(Lang.GetMessage(LangKeys.Auth.PhoneAlreadyRegistered));
            }

            var (allowed, reasonKey) = await _otpService.CheckRateLimitAsync(userDto.Email!);
            if (!allowed)
                return StatusCode(429, new ApiResponse(429, Lang.GetMessage(reasonKey!)));

            var hasher = new PasswordHasher<AppUser>();
            var passwordHash = hasher.HashPassword(null!, userDto.Password!);

            var existing = await _context.PendingRegistrations
                .Where(p => p.Email == userDto.Email!)
                .ToListAsync();
            _context.PendingRegistrations.RemoveRange(existing);

            _context.PendingRegistrations.Add(new PendingRegistration
            {
                FirstName = userDto.FirstName ?? string.Empty,
                LastName = userDto.LastName ?? string.Empty,
                Email = userDto.Email!,
                PhoneNumber = userDto.PhoneNumber,
                PasswordHash = passwordHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });

            await _context.SaveChangesAsync();

            try
            {
                var code = await _otpService.GenerateOtpAsync(userDto.Email!);
                await _emailService.SendEmailOtpAsync(userDto.Email!, userDto.FirstName ?? string.Empty, code);
            }
            catch (Exception)
            {
                var pending = await _context.PendingRegistrations
                    .FirstOrDefaultAsync(p => p.Email == userDto.Email!);
                if (pending != null) _context.PendingRegistrations.Remove(pending);
                await _context.SaveChangesAsync();
                throw new BadRequestException(Lang.GetMessage(LangKeys.Auth.RegistrationEmailFailed));
            }

            return CreatedResult(message: Lang.GetMessage(LangKeys.Auth.RegistrationSuccess));
        }
        /// <summary>Authenticates a user with email and password, returning JWT and refresh tokens.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email!);
            if (user == null)
                throw new NotFoundException(Lang.GetMessage(LangKeys.Auth.AccountNotFound));

            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException(Lang.GetMessage(LangKeys.Auth.AccountLocked));

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password!))
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.InvalidCredentials));
            }

            if (!user.EmailConfirmed)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.EmailNotVerified));

            if (string.IsNullOrEmpty(user.Code))
                await _codeAssignment.AssignUserCodeAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);

            _backgroundJobs.Enqueue<INotificationService>(ns =>
                ns.CreateNotificationAsync(
                    user.Id,
                    "New Login Detected",
                    $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                    "If this wasn't you, please change your password immediately.",
                    "Security", null, null, null));

            return await GenerateAuthResponse(user, loginDto.RememberMe);
        }

        public class GoogleLoginDto
        {
            public string IdToken { get; set; } = string.Empty;
        }
        /// <summary>Authenticates or registers a user via Google OAuth ID token.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("login")]
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            try
            {
                var payload = await _googleAuthService.VerifyGoogleTokenAsync(dto.IdToken);
                if (payload == null)
                    return Unauthorized(new { message = Lang.GetMessage(LangKeys.Auth.LoginGoogleInvalidToken) });

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
                        return BadRequest(new { message = Lang.GetMessage(LangKeys.Auth.LoginGoogleFailed), details = errors });
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
                return StatusCode(500, new Errors.ApiResult(message: "Server error"));
            }
        }
        /// <summary>Updates the Firebase Cloud Messaging push notification token for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("update-fcm-token")]
        [Authorize]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FcmToken))
                return BadRequest(new Errors.ApiResult(message: Lang.GetMessage(LangKeys.Auth.FcmEmpty)));

            var userId = GetRequiredUserId();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new Errors.ApiResult(message: Lang.GetMessage(LangKeys.User.NotFound)));

            var isNewToken = user.FcmToken != request.FcmToken;

            user.FcmToken = request.FcmToken;
            await _userManager.UpdateAsync(user);

            if (isNewToken)
            {
                _backgroundJobs.Enqueue<INotificationService>(ns =>
                    ns.CreateNotificationAsync(
                        userId,
                        "New Login Detected",
                        $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                        "If this wasn't you, please change your password immediately.",
                        "Security", null, null, null));
            }

            return OkResult(message: Lang.GetMessage(LangKeys.Auth.FcmUpdated));
        }
        /// <summary>Issues a new access token using a valid refresh token.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("refresh")]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var ipAddress = GetIpAddress();
            var oldToken = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (oldToken == null || !oldToken.IsActive)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.SessionInvalid));

            var user = await _userManager.FindByIdAsync(oldToken.UserId);
            if (user == null)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.UserNoLongerExists));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtHandler.CreateToken(user, roles);
                var newToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, ipAddress);
                await _refreshTokenService.RevokeTokenAsync(oldToken.Token, ipAddress, newToken.Token);
                await transaction.CommitAsync();
                return OkResult(data: CreateTokenResponse(accessToken, newToken.Token));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw new BadRequestException(Lang.GetMessage(LangKeys.Auth.TokenRefreshFailed));
            }
        }
        /// <summary>Sends a password reset OTP code to the user's email address.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("otp")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null)
                return OkResult(message: Lang.GetMessage(LangKeys.Password.ForgotPasswordSent));

            var (allowed, reasonKey) = await _otpService.CheckRateLimitAsync(dto.Email!, purpose: "password_reset");
            if (!allowed)
                return StatusCode(429, new ApiResponse(429, Lang.GetMessage(reasonKey!)));

            var code = await _otpService.GenerateOtpAsync(dto.Email!, purpose: "password_reset");
            await _emailService.SendEmailOtpAsync(user.Email!, user.FirstName, code);
            return OkResult(message: Lang.GetMessage(LangKeys.Password.ForgotPasswordCodeSent));
        }
        /// <summary>Resets the user's password using an email and OTP verification code.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("sensitive")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithOtpDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Password.Invalid));

            var valid = await _otpService.ValidateOtpAsync(dto.Email, dto.Code, purpose: "password_reset");
            if (!valid)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Password.CodeInvalid));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            return OkResult(message: Lang.GetMessage(LangKeys.Password.ResetSuccess));
        }
        /// <summary>Resends the email verification OTP to an unconfirmed user.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("resend-verification-email")]
        [EnableRateLimiting("otp")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email!);
            if (user == null || user.EmailConfirmed)
                return OkResult(message: Lang.GetMessage(LangKeys.Verification.Sent));

            var (allowed, reasonKey) = await _otpService.CheckRateLimitAsync(user.Email!);
            if (!allowed)
                return StatusCode(429, new ApiResponse(429, Lang.GetMessage(reasonKey!)));

            await SendVerificationEmail(user);
            return OkResult(message: Lang.GetMessage(LangKeys.Verification.NewSent));
        }
        /// <summary>Revokes a refresh token, logging the user out of that session.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            var userId = GetRequiredUserId();
            var token = await _refreshTokenService.GetRefreshTokenAsync(dto.RefreshToken);

            if (token == null || token.UserId != userId)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.InvalidCredentials));

            await _refreshTokenService.RevokeTokenAsync(dto.RefreshToken, GetIpAddress());
            return OkResult(message: Lang.GetMessage(LangKeys.Auth.LogoutSuccess));
        }
        /// <summary>Authenticates an admin user with email and password. Only admin accounts are allowed.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("login")]
        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] UserForLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email!);
            if (user == null)
                throw new NotFoundException(Lang.GetMessage(LangKeys.Auth.AccountNotFound));

            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException(Lang.GetMessage(LangKeys.Auth.AccountLocked));

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password!))
            {
                await _userManager.AccessFailedAsync(user);
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.InvalidCredentials));
            }

            if (!user.EmailConfirmed)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.EmailNotVerified));

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.NotAdmin));

            if (string.IsNullOrEmpty(user.Code))
                await _codeAssignment.AssignUserCodeAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);

            _backgroundJobs.Enqueue<INotificationService>(ns =>
                ns.CreateNotificationAsync(
                    user.Id,
                    "New Login Detected",
                    $"You logged in on {DateTime.UtcNow:MMM dd, yyyy 'at' HH:mm} UTC. " +
                    "If this wasn't you, please change your password immediately.",
                    "Security", null, null, null));

            return await GenerateAuthResponse(user, loginDto.RememberMe);
        }
        /// <summary>Gets the authenticated user's profile details.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            return OkResult(data: new
            {
                userCode = user.Code,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                profilePictureUrl = _imageService.GetFullImageUrl(user.ProfilePictureUrl!),
                profilePictureRelativePath = user.ProfilePictureUrl
            });
        }
        /// <summary>Updates the authenticated user's profile fields.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.PhoneNumber = dto.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException($"Failed to update profile: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            return OkResult(
                data: new { user.FirstName, user.LastName, user.PhoneNumber, user.Email },
                message: Lang.GetMessage(LangKeys.Profile.UpdateSuccess));
        }
        /// <summary>Uploads a profile picture for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("upload-profile-picture")]
        [Authorize]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);

            var imageUrl = await _imageService.UploadImageAsync(file, "profiles");
            user.ProfilePictureUrl = imageUrl;
            await _userManager.UpdateAsync(user);

            return OkResult(data: new
            {
                relativePath = imageUrl,
                fullUrl = _imageService.GetFullImageUrl(imageUrl)
            }, message: Lang.GetMessage(LangKeys.Profile.PictureUpdateSuccess));
        }
        /// <summary>Deletes the authenticated user's profile picture.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpDelete("delete-profile-picture")]
        [Authorize]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);
                user.ProfilePictureUrl = null;
                await _userManager.UpdateAsync(user);
            }

            return OkResult(message: Lang.GetMessage(LangKeys.Profile.PictureDeleteSuccess));
        }
        /// <summary>Changes the authenticated user's password and revokes all existing sessions.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Password.Invalid));

            await _refreshTokenService.RevokeUserTokensAsync(userId, GetIpAddress());
            return OkResult(message: Lang.GetMessage(LangKeys.Password.ChangeSuccess));
        }
        /// <summary>Verifies an email OTP code to confirm email address or complete registration.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("sensitive")]
        [HttpPost("verify-email-otp")]
        public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
                throw new BadRequestException("Email and code are required");

            var isValid = await _otpService.ValidateOtpAsync(dto.Email, dto.Code);
            if (!isValid)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Password.CodeInvalid));

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                if (!existingUser.EmailConfirmed)
                {
                    existingUser.EmailConfirmed = true;
                    await _userManager.UpdateAsync(existingUser);
                }
                return await GenerateAuthResponse(existingUser);
            }

            var pending = await _context.PendingRegistrations
                .FirstOrDefaultAsync(p => p.Email == dto.Email);

            if (pending == null)
                throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            if (pending.ExpiresAt < DateTime.UtcNow)
            {
                _context.PendingRegistrations.Remove(pending);
                await _context.SaveChangesAsync();
                throw new BadRequestException(Lang.GetMessage(LangKeys.Otp.Expired));
            }

            var user = new AppUser
            {
                FirstName = pending.FirstName,
                LastName = pending.LastName,
                Email = pending.Email,
                UserName = pending.Email,
                PhoneNumber = pending.PhoneNumber,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = pending.PasswordHash
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            await _codeAssignment.AssignUserCodeAsync(user);

            _context.PendingRegistrations.Remove(pending);
            await _context.SaveChangesAsync();

            return await GenerateAuthResponse(user);
        }
        /// <summary>Verifies a password reset OTP code without consuming it.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [EnableRateLimiting("sensitive")]
        [HttpPost("verify-reset-code")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
        {
            var isValid = await _otpService.PeekOtpAsync(dto.Email, dto.Code, purpose: "password_reset");
            if (!isValid)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Password.CodeInvalid));

            return OkResult(message: Lang.GetMessage(LangKeys.Password.ResetCodeValid));
        }
        /// <summary>Permanently deletes the authenticated user's account and all associated data.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [HttpDelete("delete-account")]
        [Authorize]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
        {
            var userId = GetRequiredUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new NotFoundException(Lang.GetMessage(LangKeys.User.NotFound));

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new UnauthorizedException(Lang.GetMessage(LangKeys.Auth.InvalidCredentials));

            await _refreshTokenService.RevokeUserTokensAsync(userId, GetIpAddress());

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                await _imageService.DeleteImageAsync(user.ProfilePictureUrl);

            var userEmail = user.Email!;
            await CleanupUserDataAsync(userId, userEmail);

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));

            return OkResult(message: Lang.GetMessage(LangKeys.Auth.AccountDeleted));
        }

        /// <summary>Registers the authenticated user as a vendor. Auto-approved if content is clean, otherwise pending admin review.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Authorize]
        [HttpPost("register-vendor")]
        public async Task<IActionResult> RegisterVendor([FromBody] Graduation.BLL.DTOs.Vendor.VendorRegisterDto dto)
        {
            var userId = GetRequiredUserId();
            var result = await _vendorService.RegisterVendorAsync(userId, dto);
            return CreatedResult(data: result);
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

            return OkResult(data: response);
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
