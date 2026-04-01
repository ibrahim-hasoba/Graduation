using FluentValidation;
using Shared.DTOs.Cart;

namespace Graduation.BLL.Validators
{
    public class UpdateCartItemDtoValidator : AbstractValidator<UpdateCartItemDto>
    {
        public UpdateCartItemDtoValidator()
        {
            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be at least 1.")
                .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100 units.");

            RuleFor(x => x.VariantIds)
                .Must(x => x == null || x.Distinct().Count() == x.Count)
                .WithMessage("Variant IDs must be unique.");
        }
    }
}