using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Shared.Validation;

internal static class AsyncValidationFilter
{
    /// <summary>
    /// Adds the async validation filter to a route group. The filter auto-discovers the body
    /// parameter type for each endpoint and resolves an <see cref="IAsyncValidator{T}"/> from DI.
    /// Endpoints without a registered validator incur zero per-request overhead.
    /// </summary>
    public static RouteGroupBuilder WithAsyncValidation(this RouteGroupBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilterFactory(CreateFilter);
    }

    /// <summary>
    /// Adds the async validation filter to a single endpoint. The filter auto-discovers the body
    /// parameter type and resolves an <see cref="IAsyncValidator{T}"/> from DI.
    /// </summary>
    public static RouteHandlerBuilder WithAsyncValidation(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilterFactory(CreateFilter);
    }

    /// <summary>
    /// Adds the async validation filter for a specific request type to a single endpoint.
    /// </summary>
    /// <typeparam name="T">The request type to validate.</typeparam>
    public static RouteHandlerBuilder WithValidationOn<T>(this RouteHandlerBuilder builder) where T : class
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

    private static IResult? ToResult(ValidatorResult result) => result.ToProblemDetails() switch
    {
        HttpValidationProblemDetails details => TypedResults.ValidationProblem(details.Errors),
        { } details => TypedResults.Problem(details),
        _ => null
    };

    private static EndpointFilterDelegate CreateFilter(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var parameters = context.MethodInfo.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            if (!IsBodyParameter(parameters[i]))
            {
                continue;
            }

            var paramType = parameters[i].ParameterType;
            var createMethod = typeof(AsyncValidationFilter)
                .GetMethod(nameof(CreateTypedFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(paramType);

            return (EndpointFilterDelegate)createMethod.Invoke(null, [i, next])!;
        }

        return next;
    }

    private static EndpointFilterDelegate CreateTypedFilter<T>(int argumentIndex, EndpointFilterDelegate next) where T : class
    {
        return async context =>
        {
            var validator = context.HttpContext.RequestServices.GetService<IAsyncValidator<T>>();
            if (validator is null || context.Arguments[argumentIndex] is not T argument)
            {
                return await next(context);
            }

            var validationContext = new ValidationContext { HttpContext = context.HttpContext };
            var result = await validator.ValidateAsync(argument, validationContext, context.HttpContext.RequestAborted);

            return ToResult(result) ?? await next(context);
        };
    }

    private static bool IsBodyParameter(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;

        if (type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(Guid)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(CancellationToken)
            || type == typeof(HttpContext)
            || type == typeof(HttpRequest)
            || type == typeof(HttpResponse))
        {
            return false;
        }

        foreach (var attribute in parameter.GetCustomAttributes(true))
        {
            if (attribute is FromServicesAttribute
                or FromRouteAttribute
                or FromQueryAttribute
                or FromHeaderAttribute
                or IFromServiceMetadata)
            {
                return false;
            }
        }

        return true;
    }

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