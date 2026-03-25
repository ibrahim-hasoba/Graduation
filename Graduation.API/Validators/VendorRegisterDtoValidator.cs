using FluentValidation;
using Shared.DTOs.Vendor;

namespace Graduation.API.Validators
{
    public class VendorRegisterDtoValidator : AbstractValidator<VendorRegisterDto>
    {
        public VendorRegisterDtoValidator()
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage("Store name is required.")
                .MinimumLength(3).WithMessage("Store name must be at least 3 characters.")
                .MaximumLength(200).WithMessage("Store name cannot exceed 200 characters.")
                .Matches(@"^[\p{L}0-9\s\-_'.&]+$").WithMessage("Store name contains invalid characters.");

            RuleFor(x => x.StoreNameAr)
                .NotEmpty().WithMessage("Arabic store name is required.")
                .MinimumLength(3).WithMessage("Arabic store name must be at least 3 characters.")
                .MaximumLength(200).WithMessage("Arabic store name cannot exceed 200 characters.");

            RuleFor(x => x.StoreDescription)
                .NotEmpty().WithMessage("Store description is required.")
                .MinimumLength(20).WithMessage("Store description must be at least 20 characters.")
                .MaximumLength(2000).WithMessage("Store description cannot exceed 2000 characters.");

            RuleFor(x => x.StoreDescriptionAr)
                .NotEmpty().WithMessage("Arabic store description is required.")
                .MinimumLength(20).WithMessage("Arabic store description must be at least 20 characters.")
                .MaximumLength(2000).WithMessage("Arabic store description cannot exceed 2000 characters.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^(\+20|0)1[0125]\d{8}$")
                .WithMessage("Please enter a valid Egyptian mobile number (e.g. 01012345678 or +201012345678).");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Address is required.")
                .MinimumLength(10).WithMessage("Please provide a more detailed address.")
                .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.");

            RuleFor(x => x.City)
                .NotEmpty().WithMessage("City is required.")
                .MinimumLength(2).WithMessage("City name must be at least 2 characters.")
                .MaximumLength(100).WithMessage("City name cannot exceed 100 characters.");

            RuleFor(x => x.LogoUrl)
                .MaximumLength(2048).WithMessage("Logo URL cannot exceed 2048 characters.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Logo URL must be a valid absolute URL.")
                .When(x => !string.IsNullOrEmpty(x.LogoUrl));

            RuleFor(x => x.BannerUrl)
                .MaximumLength(2048).WithMessage("Banner URL cannot exceed 2048 characters.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Banner URL must be a valid absolute URL.")
                .When(x => !string.IsNullOrEmpty(x.BannerUrl));

            RuleFor(x => x)
                        .Must(x =>
                        (x.Latitude.HasValue && x.Longitude.HasValue) ||
                        (!x.Latitude.HasValue && !x.Longitude.HasValue))
                        .WithMessage("Latitude and Longitude must both be provided together or both be null.");
        }
    }
}
