using GroundControl.Api.Features.Audit;
using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Features.Clients;
using GroundControl.Api.Features.ConfigEntries;
using GroundControl.Api.Features.Groups;
using GroundControl.Api.Features.PersonalAccessTokens;
using GroundControl.Api.Features.Projects;
using GroundControl.Api.Features.Roles;
using GroundControl.Api.Features.Scopes;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Features.Templates;
using GroundControl.Api.Features.Users;
using GroundControl.Api.Features.Variables;
using GroundControl.Host.Api;

namespace GroundControl.Api.Host.Modules;

[RunsAfter<AppCommonModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class FeatureHandlersModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddAuditHandlers();
        builder.Services.AddScopesHandlers();
        builder.Services.AddGroupsHandlers();
        builder.Services.AddRolesHandlers();
        builder.Services.AddTemplatesHandlers();
        builder.Services.AddProjectsHandlers();
        builder.Services.AddConfigEntriesHandlers();
        builder.Services.AddVariablesHandlers();
        builder.Services.AddSnapshotsHandlers();
        builder.Services.AddClientsHandlers();
        builder.Services.AddClientApiHandlers();
        builder.Services.AddPersonalAccessTokensHandlers();
        builder.Services.AddUsersHandlers();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        app.MapAuditEndpoints();
        app.MapScopesEndpoints();
        app.MapGroupsEndpoints();
        app.MapRolesEndpoints();
        app.MapTemplatesEndpoints();
        app.MapProjectsEndpoints();
        app.MapConfigEntriesEndpoints();
        app.MapVariablesEndpoints();
        app.MapSnapshotsEndpoints();
        app.MapClientsEndpoints();
        app.MapClientApiEndpoints();
        app.MapPersonalAccessTokensEndpoints();
        app.MapUsersEndpoints();
    }
}