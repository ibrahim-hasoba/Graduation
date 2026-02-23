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
        }
    }
}
