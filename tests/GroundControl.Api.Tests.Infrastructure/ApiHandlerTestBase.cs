using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Core.Authentication.BuiltIn;
using GroundControl.Api.Core.Authentication.NoAuth;
using GroundControl.Persistence.MongoDb;
using MongoDB.Driver;

namespace GroundControl.Api.Tests.Infrastructure;

/// <summary>
/// Provides shared helpers for API handler integration tests while preserving per-test database isolation.
/// </summary>
public abstract class ApiHandlerTestBase
{
    protected static readonly string JwtSecret = Convert.ToBase64String(
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    ]);

    protected const string SeedPassword = "Test!Password123";
    protected const string SeedEmail = "admin@test.local";
    protected const string SeedUsername = "admin";

    private readonly MongoFixture _mongoFixture;

    protected ApiHandlerTestBase(MongoFixture mongoFixture)
    {
        ArgumentNullException.ThrowIfNull(mongoFixture);

        _mongoFixture = mongoFixture;
    }

    protected static JsonSerializerOptions WebJsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    protected static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected GroundControlApiFactory CreateFactory(Dictionary<string, string?>? extraConfig = null) => new(_mongoFixture, extraConfig);

    protected static async Task<TResponse> ReadRequiredJsonAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : class
    {
        var payload = await response.Content.ReadFromJsonAsync<TResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        payload.ShouldNotBeNull();

        return payload;
    }

    protected static async Task<TResponse> ReadRequiredStreamAsync<TResponse>(Stream? stream, CancellationToken cancellationToken)
        where TResponse : class
    {
        stream.ShouldNotBeNull();
        var payload = await JsonSerializer.DeserializeAsync<TResponse>(stream, WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        payload.ShouldNotBeNull();

        return payload;
    }

    protected GroundControlApiFactory CreateApiFactoryWithBuiltInAuthentication(Dictionary<string, string?>? extraConfig = null, IMongoDatabase? existingDatabase = null)
    {
        var config = new Dictionary<string, string?>
        {
            [$"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.Mode)}"] = "BuiltIn",
            [$"{JwtOptions.SectionName}:{nameof(JwtOptions.Secret)}"] = JwtSecret,
            [$"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}"] = "GroundControl",
            [$"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}"] = "GroundControl",
            [$"{PasswordPolicyOptions.SectionName}:{nameof(PasswordPolicyOptions.RequiredLength)}"] = "8",
            [$"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminUsername)}"] = SeedUsername,
            [$"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminEmail)}"] = SeedEmail,
            [$"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminPassword)}"] = SeedPassword
        };

        if (extraConfig is not null)
        {
            foreach (var kvp in extraConfig)
            {
                config[kvp.Key] = kvp.Value;
            }
        }

        if (existingDatabase is not null)
        {
            config[$"{MongoDbOptions.SectionName}:{nameof(MongoDbOptions.DatabaseName)}"] = existingDatabase.DatabaseNamespace.DatabaseName;
        }

        return CreateFactory(config);
    }

}