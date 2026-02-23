using FluentValidation;
using Shared.DTOs.Vendor;

namespace Graduation.API.Validators
{
    public class VendorApprovalDtoValidator : AbstractValidator<VendorApprovalDto>
    {
        public VendorApprovalDtoValidator()
        {
            RuleFor(x => x.RejectionReason)
                .NotEmpty().WithMessage("A rejection reason is required when denying a vendor.")
                .MaximumLength(500).WithMessage("Rejection reason cannot exceed 500 characters.")
                .When(x => !x.IsApproved);
        }
    }
}
