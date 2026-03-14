namespace GroundControl.Api.Shared.Validation;

internal static class AsyncValidationFilter
{
    /// <summary>
    /// Adds the async validation filter for a specific request type to a single endpoint.
    /// </summary>
    /// <typeparam name="T">The request type to validate.</typeparam>
    public static RouteHandlerBuilder WithContractValidation<T>(this RouteHandlerBuilder builder) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var validator = context.HttpContext.RequestServices.GetService<IAsyncValidator<T>>();
            if (validator is null)
            {
                return await next(context);
            }

            var argument = FindArgument<T>(context.Arguments);
            if (argument is null)
            {
                return await next(context);
            }

            var validationContext = new ValidationContext { HttpContext = context.HttpContext };
            var result = await validator.ValidateAsync(argument, validationContext, context.HttpContext.RequestAborted);

            return ToResult(result) ?? await next(context);
        });
    }

    /// <summary>
    /// Adds the async validation filter for an endpoint validator resolved by its concrete type.
    /// </summary>
    /// <typeparam name="TValidator">The endpoint validator type to resolve from DI.</typeparam>
    public static RouteHandlerBuilder WithEndpointValidation<TValidator>(this RouteHandlerBuilder builder)
        where TValidator : class, IEndpointValidator
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var validator = context.HttpContext.RequestServices.GetService<TValidator>();
            if (validator is null)
            {
                return await next(context);
            }

            var validationContext = new ValidationContext { HttpContext = context.HttpContext };
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            return ToResult(result) ?? await next(context);
        });
    }

    private static IResult? ToResult(ValidatorResult result) => result.ToProblemDetails() switch
    {
        HttpValidationProblemDetails details => TypedResults.ValidationProblem(details.Errors),
        { } details => TypedResults.Problem(details),
        _ => null
    };

    private static T? FindArgument<T>(IList<object?> arguments) where T : class
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] is T match)
            {
                return match;
            }
        }

        return null;
    }
}