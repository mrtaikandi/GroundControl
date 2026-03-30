using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Shared.Security;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Audit;

[RunsAfter<AppCommonModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class AuditModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<ListAuditRecordsHandler>();
        builder.Services.AddTransient<GetAuditRecordHandler>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/audit-records")
            .WithTags("Audit")
            .RequireAuthorization(Permissions.AuditRead);

        ListAuditRecordsHandler.Endpoint(group);
        GetAuditRecordHandler.Endpoint(group);
    }
}