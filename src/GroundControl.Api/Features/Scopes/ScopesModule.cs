using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Scopes;

[RunsAfter<AppCommonModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class ScopesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateScopeHandler>();
        builder.Services.AddTransient<GetScopeHandler>();
        builder.Services.AddTransient<ListScopesHandler>();
        builder.Services.AddTransient<UpdateScopeHandler>();
        builder.Services.AddTransient<DeleteScopeHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateScopeRequest>, CreateScopeValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateScopeRequest>, UpdateScopeValidator>();
        builder.Services.AddTransient<DeleteScopeValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/scopes")
            .WithTags("Scopes");

        CreateScopeHandler.Endpoint(group);
        GetScopeHandler.Endpoint(group);
        ListScopesHandler.Endpoint(group);
        UpdateScopeHandler.Endpoint(group);
        DeleteScopeHandler.Endpoint(group);
    }
}