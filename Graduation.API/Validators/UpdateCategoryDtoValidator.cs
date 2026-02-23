using FluentValidation;
using Shared.DTOs.Category;

namespace Graduation.API.Validators
{
    public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
    {
        public UpdateCategoryDtoValidator()
        {
            RuleFor(x => x.NameEn)
                .MinimumLength(2).WithMessage("English name must be at least 2 characters.")
                .MaximumLength(100).WithMessage("English name cannot exceed 100 characters.")
                .Matches(@"^[\w\s\-&'.]+$").WithMessage("English name contains invalid characters.")
                .When(x => !string.IsNullOrEmpty(x.NameEn));

            RuleFor(x => x.NameAr)
                .MinimumLength(2).WithMessage("Arabic name must be at least 2 characters.")
                .MaximumLength(100).WithMessage("Arabic name cannot exceed 100 characters.")
                .When(x => !string.IsNullOrEmpty(x.NameAr));

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
                .When(x => x.Description != null);

            RuleFor(x => x.ImageUrl)
                .MaximumLength(2048).WithMessage("Image URL cannot exceed 2048 characters.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Image URL must be a valid absolute URL.")
                .When(x => !string.IsNullOrEmpty(x.ImageUrl));

            RuleFor(x => x.ParentCategoryId)
                .GreaterThan(0).WithMessage("Parent category ID must be a positive integer.")
                .When(x => x.ParentCategoryId.HasValue && x.ParentCategoryId.Value != 0);
        }
    }
}
