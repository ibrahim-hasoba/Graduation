using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Graduation.API.Filters
{
    /// <summary>
    /// Runs any registered FluentValidation validators for action arguments and populates ModelState.
    /// </summary>
    public class FluentValidationFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var serviceProvider = context.HttpContext.RequestServices;

            foreach (var arg in context.ActionArguments)
            {
                var argType = arg.Value?.GetType();
                if (argType == null) continue;

                var validatorType = typeof(IValidator<>).MakeGenericType(argType);
                var validator = serviceProvider.GetService(validatorType) as IValidator;
                if (validator == null) continue;

                var validationContextType = typeof(ValidationContext<>).MakeGenericType(argType);
                var validationContext = Activator.CreateInstance(validationContextType, arg.Value) as IValidationContext;
                if (validationContext == null) continue;

                var result = await validator.ValidateAsync(validationContext as dynamic);
                if (!result.IsValid)
                {
                    foreach (var failure in result.Errors)
                    {
                        context.ModelState.AddModelError(failure.PropertyName ?? string.Empty, failure.ErrorMessage);
                    }
                }
            }

            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(m => m.Value!.Errors.Count > 0)
                    .SelectMany(m => m.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToArray();

                var errorResponse = new Shared.Errors.ApiValidationErrorResponse { Errors = errors };
                context.Result = new BadRequestObjectResult(errorResponse);
                return;
            }

            await next();
        }
    }
}
