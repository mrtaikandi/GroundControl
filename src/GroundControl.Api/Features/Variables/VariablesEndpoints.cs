using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Variables;

internal static class VariablesEndpoints
{
    public static IServiceCollection AddVariablesHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateVariableHandler>();
        services.AddTransient<GetVariableHandler>();
        services.AddTransient<ListVariablesHandler>();
        services.AddTransient<UpdateVariableHandler>();
        services.AddTransient<DeleteVariableHandler>();

        services.AddTransient<IAsyncValidator<CreateVariableRequest>, CreateVariableValidator>();
        services.AddTransient<IAsyncValidator<UpdateVariableRequest>, UpdateVariableValidator>();
        services.AddTransient<DeleteVariableValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapVariablesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/variables")
            .WithTags("Variables");

        CreateVariableHandler.Endpoint(group);
        GetVariableHandler.Endpoint(group);
        ListVariablesHandler.Endpoint(group);
        UpdateVariableHandler.Endpoint(group);
        DeleteVariableHandler.Endpoint(group);

        return endpoints;
    }
}