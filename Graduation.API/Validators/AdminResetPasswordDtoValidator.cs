using FluentValidation;
using Shared.DTOs.Admin;

namespace Graduation.API.Validators
{
    public class AdminResetPasswordDtoValidator : AbstractValidator<AdminResetPasswordDto>
    {
        public AdminResetPasswordDtoValidator()
        {
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters long")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches("[0-9]").WithMessage("Password must contain at least one number")
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
        }
    }
}
