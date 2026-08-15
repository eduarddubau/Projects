using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Backend.Filters;

// Runs after [ApiController]'s binding check, so a body that failed to bind
// never reaches a validator.
public class FluentValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;
    private readonly ProblemDetailsFactory _problemDetails;

    public FluentValidationFilter(IServiceProvider services, ProblemDetailsFactory problemDetails)
    {
        _services = services;
        _problemDetails = problemDetails;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            // Runtime type, not the declared one; route Guids have no validator.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted
            );

            foreach (var failure in result.Errors)
                context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            // The factory, not a bare ValidationProblemDetails: only it matches the
            // 400 [ApiController] produces for binding failures.
            context.Result = new BadRequestObjectResult(
                _problemDetails.CreateValidationProblemDetails(
                    context.HttpContext,
                    context.ModelState
                )
            );
            return;
        }

        await next();
    }
}
