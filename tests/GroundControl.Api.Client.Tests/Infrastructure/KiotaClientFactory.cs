namespace GroundControl.Api.Client.Tests.Infrastructure;

internal static class ApiClientFactory
{
    public static (GroundControlClient Client, ResponseCapturingHandler Handler) Create(GroundControlApiFactory factory)
    {
        var handler = new ResponseCapturingHandler();
        var httpClient = factory.CreateDefaultClient(handler);
        return (new GroundControlClient(httpClient), handler);
    }
}