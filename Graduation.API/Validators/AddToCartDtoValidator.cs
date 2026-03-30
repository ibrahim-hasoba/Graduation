using FluentValidation;
using Shared.DTOs.Cart;

namespace Graduation.API.Validators
{
    public class AddToCartDtoValidator : AbstractValidator<AddToCartDto>
    {
        public AddToCartDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("A valid product ID is required.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be at least 1.")
                .LessThanOrEqualTo(100).WithMessage("Cannot add more than 100 units of a single product at once.");

            RuleFor(x => x.VariantId)
                .GreaterThan(0).WithMessage("Variant ID must be a positive integer.")
                .When(x => x.VariantId.HasValue);
        }
    }
}
