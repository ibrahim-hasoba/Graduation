using FluentValidation;
using Shared.DTOs.Product;

namespace Graduation.API.Validators
{
    public class ProductUpdateDtoValidator : AbstractValidator<ProductUpdateDto>
    {
        public ProductUpdateDtoValidator()
        {
            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("Arabic product name is required.")
                .MinimumLength(3).WithMessage("Arabic name must be at least 3 characters.")
                .MaximumLength(300).WithMessage("Arabic name cannot exceed 300 characters.");

            RuleFor(x => x.NameEn)
                .NotEmpty().WithMessage("English product name is required.")
                .MinimumLength(3).WithMessage("English name must be at least 3 characters.")
                .MaximumLength(300).WithMessage("English name cannot exceed 300 characters.");

            RuleFor(x => x.DescriptionAr)
                .NotEmpty().WithMessage("Arabic description is required.")
                .MinimumLength(10).WithMessage("Arabic description must be at least 10 characters.")
                .MaximumLength(5000).WithMessage("Arabic description cannot exceed 5000 characters.");

            RuleFor(x => x.DescriptionEn)
                .NotEmpty().WithMessage("English description is required.")
                .MinimumLength(10).WithMessage("English description must be at least 10 characters.")
                .MaximumLength(5000).WithMessage("English description cannot exceed 5000 characters.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Price must be greater than 0.")
                .LessThanOrEqualTo(1_000_000).WithMessage("Price cannot exceed 1,000,000.");

            RuleFor(x => x.DiscountPrice)
                .GreaterThan(0).WithMessage("Discount price must be greater than 0.")
                .LessThan(x => x.Price).WithMessage("Discount price must be less than the regular price.")
                .When(x => x.DiscountPrice.HasValue);

            RuleFor(x => x.StockQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative.")
                .LessThanOrEqualTo(100_000).WithMessage("Stock quantity cannot exceed 100,000.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("A valid category must be selected.");

            RuleFor(x => x.MadeInCity)
                .MinimumLength(2).WithMessage("City name must be at least 2 characters.")
                .MaximumLength(100).WithMessage("City name cannot exceed 100 characters.")
                .When(x => !string.IsNullOrEmpty(x.MadeInCity));

            RuleFor(x => x.MadeInGovernorate)
                .IsInEnum().WithMessage("A valid Egyptian governorate must be selected.")
                .When(x => x.MadeInGovernorate.HasValue);
        }
    }
}
