namespace Graduation.API.Validators
{
    using FluentValidation;
    using Microsoft.AspNetCore.Http.HttpResults;
    using Microsoft.SqlServer.Server;
    using Shared.DTOs.Auth;
    using System.Diagnostics.Metrics;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    namespace Graduation.API.Validators
    {
        public class ResetPasswordWithOtpDtoValidator : AbstractValidator<ResetPasswordWithOtpDto>
        {
            public ResetPasswordWithOtpDtoValidator()
            {
                RuleFor(x => x.Email)
                    .NotEmpty().WithMessage("Email is required.")
                    .EmailAddress().WithMessage("A valid email address is required.");

                RuleFor(x => x.Code)
                    .NotEmpty().WithMessage("Verification code is required.")
                    .Length(6).WithMessage("Code must be exactly 6 digits.")
                    .Matches(@"^\d{6}$").WithMessage("Code must contain only digits.");

                RuleFor(x => x.NewPassword)
                    .NotEmpty().WithMessage("New password is required.")
                    .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                    .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                    .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                    .Matches(@"\d").WithMessage("Password must contain at least one digit.");

                RuleFor(x => x.ConfirmPassword)
                    .NotEmpty().WithMessage("Confirm password is required.")
                    .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
            }
        }
    }
}
