using FluentValidation;
using Shared.DTOs;

namespace Graduation.API.Validators
{
    public class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
    {
        public UpdateProfileDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MinimumLength(2).WithMessage("First name must be at least 2 characters.")
                .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.")
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes.");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MinimumLength(2).WithMessage("Last name must be at least 2 characters.")
                .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.")
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes.");

            RuleFor(x => x.PhoneNumber)
           .Matches(@"^(?:\+20|0020)?0?1[0125]\d{8}$")
           .WithMessage("If provided, the phone number must be a valid Egyptian number.")
           .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        }
    }
}
