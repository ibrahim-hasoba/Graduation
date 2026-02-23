using FluentValidation;
using Shared.DTOs.Order;

namespace Graduation.API.Validators
{
    public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
    {
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

            RuleFor(x => x.ShippingAddress)
                .NotEmpty().WithMessage("Shipping address is required.")
                .MinimumLength(10).WithMessage("Please provide a more detailed address (at least 10 characters).")
                .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.");

            RuleFor(x => x.ShippingCity)
                .NotEmpty().WithMessage("City is required.")
                .MinimumLength(2).WithMessage("City name must be at least 2 characters.")
                .MaximumLength(100).WithMessage("City name cannot exceed 100 characters.");

            RuleFor(x => x.ShippingGovernorate)
                .IsInEnum().WithMessage("A valid Egyptian governorate must be selected.");

            RuleFor(x => x.ShippingPhone)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^(\+20|0)1[0125]\d{8}$")
                .WithMessage("Please enter a valid Egyptian mobile number (e.g. 01012345678 or +201012345678).");

            RuleFor(x => x.PaymentMethod)
                .IsInEnum().WithMessage("A valid payment method must be selected.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Order notes cannot exceed 500 characters.")
                .When(x => x.Notes != null);
        }
    }
}
