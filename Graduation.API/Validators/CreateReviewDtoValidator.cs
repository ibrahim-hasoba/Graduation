using FluentValidation;
using Shared.DTOs.Review;

namespace Graduation.API.Validators
{
    public class CreateReviewDtoValidator : AbstractValidator<CreateReviewDto>
    {
        public CreateReviewDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("A valid product ID is required.");

            RuleFor(x => x.Rating)
                .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

            RuleFor(x => x.Comment)
                .MaximumLength(1000).WithMessage("Comment cannot exceed 1000 characters.")
                .MinimumLength(10).WithMessage("Comment must be at least 10 characters if provided.")
                .When(x => !string.IsNullOrEmpty(x.Comment));
        }
    }
}
