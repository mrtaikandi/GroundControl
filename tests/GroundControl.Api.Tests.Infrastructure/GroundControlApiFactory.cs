using GroundControl.Api.Core.Authentication;
using GroundControl.Persistence.MongoDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using MongoDB.Driver;
using Xunit;

namespace GroundControl.Api.Tests.Infrastructure;

public sealed class GroundControlApiFactory : WebApplicationFactory<Program>
{
    private readonly IMongoDatabase _database;
    private readonly MongoFixture _mongoFixture;
    private readonly Dictionary<string, string?> _extraConfig;

    public GroundControlApiFactory(MongoFixture mongoFixture, Dictionary<string, string?>? extraConfig = null)
    {
        _mongoFixture = mongoFixture ?? throw new ArgumentNullException(nameof(mongoFixture));
        _database = _mongoFixture.CreateDatabase();
        _extraConfig = extraConfig ?? [];
    }

    public IMongoDatabase Database => _database;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:Storage", _mongoFixture.ConnectionString);
        builder.UseSetting($"{MongoDbOptions.SectionName}:{nameof(MongoDbOptions.DatabaseName)}", _database.DatabaseNamespace.DatabaseName);
        builder.UseSetting($"{AuthenticationOptions.SectionName}:{nameof(AuthenticationOptions.Mode)}", nameof(AuthenticationMode.None));

        foreach (var kvp in _extraConfig)
        {
            builder.UseSetting(kvp.Key, kvp.Value);
        }

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging(c => c.OutputSink = message => TestContext.Current.TestOutputHelper?.WriteLine(message));
            logging.AddFilter<FakeLoggerProvider>(l => l >= LogLevel.Debug);
        });
    }
}