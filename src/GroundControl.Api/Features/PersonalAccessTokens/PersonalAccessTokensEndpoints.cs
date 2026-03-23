using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal static class PersonalAccessTokensEndpoints
{
    public static IServiceCollection AddPersonalAccessTokensHandlers(this IServiceCollection services)
    {
        services.AddTransient<CreatePatHandler>();
        services.AddTransient<ListPatsHandler>();
        services.AddTransient<GetPatHandler>();
        services.AddTransient<RevokePatHandler>();

        services.AddTransient<IAsyncValidator<CreatePatRequest>, CreatePatValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapPersonalAccessTokensEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/personal-access-tokens")
            .WithTags("PersonalAccessTokens");

        CreatePatHandler.Endpoint(group);
        ListPatsHandler.Endpoint(group);
        GetPatHandler.Endpoint(group);
        RevokePatHandler.Endpoint(group);

        return endpoints;
    }
}