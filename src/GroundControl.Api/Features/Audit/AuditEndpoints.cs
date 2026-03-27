using GroundControl.Api.Shared.Security;

namespace GroundControl.Api.Features.Audit;

internal static class AuditEndpoints
{
    public static IServiceCollection AddAuditHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ListAuditRecordsHandler>();
        services.AddTransient<GetAuditRecordHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/audit-records")
            .WithTags("Audit")
            .RequireAuthorization(Permissions.AuditRead);

        ListAuditRecordsHandler.Endpoint(group);
        GetAuditRecordHandler.Endpoint(group);

        return endpoints;
    }
}