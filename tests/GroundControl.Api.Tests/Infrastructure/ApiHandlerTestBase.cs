using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Infrastructure;

/// <summary>
/// Provides shared helpers for API handler integration tests while preserving per-test database isolation.
/// </summary>
public abstract class ApiHandlerTestBase
{
    private readonly MongoFixture _mongoFixture;

    protected ApiHandlerTestBase(MongoFixture mongoFixture)
    {
        ArgumentNullException.ThrowIfNull(mongoFixture);

        _mongoFixture = mongoFixture;
    }

    protected static JsonSerializerOptions WebJsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    protected static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected GroundControlApiFactory CreateFactory() => new(_mongoFixture);

    protected static async Task<TResponse> ReadRequiredJsonAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : class
    {
        var payload = await response.Content.ReadFromJsonAsync<TResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        payload.ShouldNotBeNull();

        return payload!;
    }
}