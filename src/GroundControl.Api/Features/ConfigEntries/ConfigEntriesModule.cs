using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.ConfigEntries;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class ConfigEntriesModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateConfigEntryHandler>();
        builder.Services.AddTransient<GetConfigEntryHandler>();
        builder.Services.AddTransient<ListConfigEntriesHandler>();
        builder.Services.AddTransient<UpdateConfigEntryHandler>();
        builder.Services.AddTransient<DeleteConfigEntryHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateConfigEntryRequest>, CreateConfigEntryValidator>();
        builder.Services.AddTransient<IAsyncValidator<UpdateConfigEntryRequest>, UpdateConfigEntryValidator>();
        builder.Services.AddTransient<DeleteConfigEntryValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/config-entries")
            .WithTags("ConfigEntries");

        CreateConfigEntryHandler.Endpoint(group);
        GetConfigEntryHandler.Endpoint(group);
        ListConfigEntriesHandler.Endpoint(group);
        UpdateConfigEntryHandler.Endpoint(group);
        DeleteConfigEntryHandler.Endpoint(group);
    }
}