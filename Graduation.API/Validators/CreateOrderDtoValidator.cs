using FluentValidation;
using Shared.DTOs.Order;

namespace Graduation.API.Validators
{
    public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
    {
        private static readonly string[] _allowedClientTypes = { "web", "mobile" };
        public CreateOrderDtoValidator()
        {
            RuleFor(x => x.ShippingFirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MinimumLength(2).WithMessage("First name must be at least 2 characters.")
                .MaximumLength(50).WithMessage("First name cannot exceed 50 characters.")
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("First name contains invalid characters.");

            RuleFor(x => x.ShippingLastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MinimumLength(2).WithMessage("Last name must be at least 2 characters.")
                .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.")
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("Last name contains invalid characters.");

            RuleFor(x => x.ShippingPhone)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^(\+20|0)1[0125]\d{8}$")
                .WithMessage("Please enter a valid Egyptian mobile number (e.g. 01012345678 or +201012345678).");

            RuleFor(x => x)
                .Must(x => x.AddressId.HasValue || (x.Latitude.HasValue && x.Longitude.HasValue))
                .WithMessage("Please provide either a saved address ID or GPS coordinates (latitude & longitude).");

            RuleFor(x => x.AddressId)
                .GreaterThan(0).WithMessage("Address ID must be a positive integer.")
                .When(x => x.AddressId.HasValue);

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90.0, 90.0).WithMessage("Latitude must be between -90 and 90.")
                .When(x => x.Latitude.HasValue);

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180.0, 180.0).WithMessage("Longitude must be between -180 and 180.")
                .When(x => x.Longitude.HasValue);

            RuleFor(x => x.ShippingAddress)
                .MaximumLength(500).WithMessage("Address text cannot exceed 500 characters.")
                .When(x => x.ShippingAddress != null);

            RuleFor(x => x.ClientType)
                .Must(ct => _allowedClientTypes.Contains(ct?.ToLower()))
                .WithMessage("ClientType must be 'web' or 'mobile'.")
                .When(x => x.ClientType != null);

            RuleFor(x => x.PaymentMethod)
                .IsInEnum().WithMessage("A valid payment method must be selected.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Order notes cannot exceed 500 characters.")
                .When(x => x.Notes != null);
        }
    }
}
