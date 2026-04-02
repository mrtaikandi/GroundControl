using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace GroundControl.Api.Client.Tests.Infrastructure;

internal static class KiotaClientFactory
{
    public static (GroundControlApiClient Client, ResponseCapturingHandler Handler) Create(GroundControlApiFactory factory)
    {
        var handler = new ResponseCapturingHandler();
        var httpClient = factory.CreateDefaultClient(handler);
        var authProvider = new AnonymousAuthenticationProvider();

        // The adapter is owned by the GroundControlApiClient and shares the HttpClient lifetime
        // with the test's GroundControlApiFactory, which disposes everything on test teardown.
#pragma warning disable CA2000
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
#pragma warning restore CA2000
        adapter.BaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";

        return (new GroundControlApiClient(adapter), handler);
    }
}