using FluentValidation;
using Shared.DTOs.Notification;

namespace Graduation.API.Validators
{
    public class BulkDeleteNotificationsDtoValidator : AbstractValidator<BulkDeleteNotificationsDto>
    {
        public BulkDeleteNotificationsDtoValidator()
        {
            RuleFor(x => x.Ids)
                .NotNull().WithMessage("Ids list cannot be null.")
                .Must(ids => ids.Count <= 100).WithMessage("Cannot bulk-delete more than 100 notifications at once.")
                .Must(ids => ids.All(id => id > 0)).WithMessage("All notification IDs must be positive integers.")
                .When(x => x.Ids != null && x.Ids.Count > 0);
        }
    }
}
