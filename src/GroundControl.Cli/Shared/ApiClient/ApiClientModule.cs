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
    private const string TokenClientName = "GroundControl.TokenClient";

    /// <inheritdoc />
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        var section = context.Configuration.GetSection(SectionName);
        services.AddOptions<GroundControlClientOptions>()
            .Bind(section);

        services.AddOptions<AuthOptions>()
            .Bind(section.GetSection(AuthSectionName));

        var serverUrl = section.GetValue<string>(nameof(GroundControlClientOptions.ServerUrl));

        // Token cache and client for JWT credential flow.
        // The token client uses a separate named HttpClient to avoid infinite recursion
        // (the main pipeline's AuthenticatingHandler would try to authenticate token refresh requests).
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TokenCache>();
        services.AddHttpClient(TokenClientName, httpClient =>
        {
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                httpClient.BaseAddress = new Uri(serverUrl);
            }
        });

        services.AddSingleton<ITokenClient>(sp =>
            new TokenClient(sp.GetRequiredService<IHttpClientFactory>(), TokenClientName));

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