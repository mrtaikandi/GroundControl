using GroundControl.Api.Client;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Cli.Shared.ApiClient;

/// <summary>
/// Registers the Kiota-generated <see cref="GroundControlApiClient" /> with the DI container
/// via <see cref="IHttpClientFactory" />.
/// </summary>
internal sealed class ApiClientModule : IDependencyModule
{
    /// <summary>
    /// The configuration section name for GroundControl client options.
    /// </summary>
    internal const string SectionName = "GroundControl";

    /// <inheritdoc />
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        var section = context.Configuration.GetSection(SectionName);
        services.AddOptions<GroundControlClientOptions>()
            .Bind(section);

        var serverUrl = section.GetValue<string>(nameof(GroundControlClientOptions.ServerUrl));

        services.AddGroundControlApiClient(httpClient =>
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
        .AddHttpMessageHandler<ProblemDetailsDelegatingHandler>();

        services.AddTransient<ApiVersionHandler>();
        services.AddTransient<ProblemDetailsDelegatingHandler>();
    }
}