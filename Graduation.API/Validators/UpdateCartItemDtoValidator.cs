using FluentValidation;
using Shared.DTOs.Cart;

namespace Graduation.API.Validators
{
    public class UpdateCartItemDtoValidator : AbstractValidator<UpdateCartItemDto>
    {
        public UpdateCartItemDtoValidator()
        {
            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be at least 1.")
                .LessThanOrEqualTo(100).WithMessage("Quantity cannot exceed 100 units.");
        }
    }
}
