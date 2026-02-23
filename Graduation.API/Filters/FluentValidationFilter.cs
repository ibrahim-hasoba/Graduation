using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Errors;

namespace Graduation.API.Filters
{
    public class FluentValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var serviceProvider = context.HttpContext.RequestServices;

            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null) continue;

                var argType = argument.GetType();

                var validatorType = typeof(IValidator<>).MakeGenericType(argType);
                if (serviceProvider.GetService(validatorType) is not IValidator validator)
                    continue;
                var validationContextType = typeof(ValidationContext<>).MakeGenericType(argType);
                var validationContext = (IValidationContext)Activator.CreateInstance(validationContextType, argument)!;

                var result = await validator.ValidateAsync(validationContext);

                if (result.IsValid) continue;

                foreach (var failure in result.Errors)
                    context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage))
                    .ToArray();

                var errorResponse = new ApiValidationErrorResponse { Errors = errors };
                context.Result = new BadRequestObjectResult(errorResponse);
                return;
            }

            await next();
        }
    }
}
