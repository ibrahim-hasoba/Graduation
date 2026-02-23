using FluentValidation;
using Auth.DTOs;

namespace Graduation.API.Validators
{
    public class VerifyEmailOtpDtoValidator : AbstractValidator<VerifyEmailOtpDto>
    {
        public VerifyEmailOtpDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("A valid email address is required.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Verification code is required.")
                .Length(6).WithMessage("Verification code must be exactly 6 digits.")
                .Matches(@"^\d{6}$").WithMessage("Verification code must contain only digits.");
        }
    }
}
