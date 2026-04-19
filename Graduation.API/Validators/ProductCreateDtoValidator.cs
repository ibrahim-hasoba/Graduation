using FluentValidation;
using Shared.DTOs.Product;

namespace Graduation.API.Validators
{
    public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
    {
        public ProductCreateDtoValidator()
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

            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("SKU is required.")
                .MinimumLength(3).WithMessage("SKU must be at least 3 characters.")
                .MaximumLength(100).WithMessage("SKU cannot exceed 100 characters.")
                .Matches(@"^[A-Za-z0-9\-_]+$").WithMessage("SKU can only contain letters, numbers, hyphens, and underscores.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("A valid category must be selected.");

           

            RuleFor(x => x.MadeInGovernorate)
                .IsInEnum().WithMessage("A valid Egyptian governorate must be selected.")
                .When(x => x.MadeInGovernorate.HasValue);

            RuleFor(x => x.ImageUrls)
                .Must(urls => urls == null || urls.Count <= 5)
                .WithMessage("A product can have at most 5 images.")
                .Must(urls => urls == null || urls.All(u => Uri.TryCreate(u, UriKind.Absolute, out _)))
                .WithMessage("All image URLs must be valid absolute URLs.")
                .When(x => x.ImageUrls != null);
        }
    }
}
