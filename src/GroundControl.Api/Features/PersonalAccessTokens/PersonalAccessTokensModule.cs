using GroundControl.Api.Features.PersonalAccessTokens.Contracts;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.PersonalAccessTokens;

internal sealed class PersonalAccessTokensModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreatePatHandler>();
        builder.Services.AddTransient<ListPatsHandler>();
        builder.Services.AddTransient<GetPatHandler>();
        builder.Services.AddTransient<RevokePatHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreatePatRequest>, CreatePatValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/personal-access-tokens")
            .WithTags("PersonalAccessTokens");

        CreatePatHandler.Endpoint(group);
        ListPatsHandler.Endpoint(group);
        GetPatHandler.Endpoint(group);
        RevokePatHandler.Endpoint(group);
    }
}