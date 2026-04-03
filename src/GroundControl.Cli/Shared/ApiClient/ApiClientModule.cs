using GroundControl.Api.Client;
using GroundControl.Api.Client.Handlers;
using GroundControl.Cli.Shared.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Cli.Shared.ApiClient;

/// <summary>
/// Registers the NSwag-generated <see cref="GroundControlClient" /> with the DI container
/// via <see cref="IHttpClientFactory" />.
/// </summary>
internal sealed class ApiClientModule : IDependencyModule
{
    /// <summary>
    /// The configuration section name for GroundControl client options.
    /// </summary>
    internal const string SectionName = "GroundControl";

    private const string AuthSectionName = "Auth";

    /// <inheritdoc />
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        var section = context.Configuration.GetSection(SectionName);
        services.AddOptions<GroundControlClientOptions>()
            .Bind(section);

        services.AddOptions<AuthOptions>()
            .Bind(section.GetSection(AuthSectionName));

        var serverUrl = section.GetValue<string>(nameof(GroundControlClientOptions.ServerUrl));

        services.AddTransient<ApiVersionHandler>();
        services.AddTransient<AuthenticatingHandler>();
        services.AddGroundControlClient(httpClient =>
            {
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    throw new InvalidOperationException(
                        "GroundControl server URL is not configured. " +
                        "Set 'GroundControl:ServerUrl' in appsettings.json, " +
                        "use the 'GroundControl__ServerUrl' environment variable, " +
                        "or run 'groundcontrol config import'.");
                }

                httpClient.BaseAddress = new Uri(serverUrl);
            })
            .AddHttpMessageHandler<ApiVersionHandler>()
            .AddHttpMessageHandler<AuthenticatingHandler>();
    }
}