using FluentValidation;
using Shared.DTOs.Address;

namespace Graduation.API.Validators
{
    public class UserAddressDtoValidator : AbstractValidator<UserAddressDto>
    {
        public UserAddressDtoValidator()
        {
            RuleFor(x => x.Nickname)
                .NotEmpty().WithMessage("Address nickname is required.")
                .MaximumLength(50).WithMessage("Nickname cannot exceed 50 characters.");

            RuleFor(x => x.FullAddress)
                .NotEmpty().WithMessage("Full address is required.")
                .MinimumLength(10).WithMessage("Please provide a more detailed address.")
                .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90 and 90.")
                .When(x => x.Latitude.HasValue);

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180 and 180.")
                .When(x => x.Longitude.HasValue);

            RuleFor(x => x.PhoneNumber)
                        .Matches(@"^(\+20|0)1[0125]\d{8}$")
                        .WithMessage("If provided, must be a valid Egyptian mobile number.")
                        .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        }
    }
}
