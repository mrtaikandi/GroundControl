using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Host.Api;

namespace GroundControl.Api.Features.Clients;

[RunsAfter<ApplicationModule>(Required = true)]
[RunsAfter<AuthenticationModule>(Required = true)]
internal sealed class ClientsModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<CreateClientHandler>();
        builder.Services.AddTransient<GetClientHandler>();
        builder.Services.AddTransient<ListClientsHandler>();
        builder.Services.AddTransient<UpdateClientHandler>();
        builder.Services.AddTransient<DeleteClientHandler>();

        builder.Services.AddTransient<IAsyncValidator<CreateClientRequest>, CreateClientValidator>();
        builder.Services.AddTransient<DeleteClientValidator>();
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/clients")
            .WithTags("Clients");

        CreateClientHandler.Endpoint(group);
        GetClientHandler.Endpoint(group);
        ListClientsHandler.Endpoint(group);
        UpdateClientHandler.Endpoint(group);
        DeleteClientHandler.Endpoint(group);
    }
}