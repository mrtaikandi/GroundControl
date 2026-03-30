using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Roles;

[RunsAfter<AppCommonModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class RolesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateRoleHandler>();
        builder.Services.AddTransient<GetRoleHandler>();
        builder.Services.AddTransient<ListRolesHandler>();
        builder.Services.AddTransient<UpdateRoleHandler>();
        builder.Services.AddTransient<DeleteRoleHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateRoleRequest>, CreateRoleValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateRoleRequest>, UpdateRoleValidator>();
        builder.Services.AddTransient<DeleteRoleValidator>();

        builder.Services.AddHostedService<RoleSeedService>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles");

        CreateRoleHandler.Endpoint(group);
        GetRoleHandler.Endpoint(group);
        ListRolesHandler.Endpoint(group);
        UpdateRoleHandler.Endpoint(group);
        DeleteRoleHandler.Endpoint(group);
    }
}