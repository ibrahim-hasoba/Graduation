using FluentValidation;
using Graduation.DAL.Entities;
using Shared.DTOs.Order;

namespace Graduation.API.Validators
{
    public class UpdateOrderStatusDtoValidator : AbstractValidator<UpdateOrderStatusDto>
    {
        public UpdateOrderStatusDtoValidator()
        {
            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("A valid order status must be provided.");

            RuleFor(x => x.CancellationReason)
                .NotEmpty().WithMessage("A cancellation reason is required when cancelling an order.")
                .MaximumLength(500).WithMessage("Cancellation reason cannot exceed 500 characters.")
                .When(x => x.Status == OrderStatus.Cancelled);
        }
    }
}
