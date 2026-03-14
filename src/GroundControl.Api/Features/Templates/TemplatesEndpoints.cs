using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Validation;

namespace GroundControl.Api.Features.Templates;

internal static class TemplatesEndpoints
{
    public static IServiceCollection AddTemplatesHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<CreateTemplateHandler>();
        services.AddTransient<GetTemplateHandler>();
        services.AddTransient<ListTemplatesHandler>();
        services.AddTransient<UpdateTemplateHandler>();
        services.AddTransient<DeleteTemplateHandler>();

        services.AddTransient<IAsyncValidator<CreateTemplateRequest>, CreateTemplateValidator>();
        services.AddTransient<IAsyncValidator<UpdateTemplateRequest>, UpdateTemplateValidator>();
        services.AddTransient<DeleteTemplateValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapTemplatesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/templates")
            .WithTags("Templates");

        CreateTemplateHandler.Endpoint(group);
        GetTemplateHandler.Endpoint(group);
        ListTemplatesHandler.Endpoint(group);
        UpdateTemplateHandler.Endpoint(group);
        DeleteTemplateHandler.Endpoint(group);

        return endpoints;
    }
}