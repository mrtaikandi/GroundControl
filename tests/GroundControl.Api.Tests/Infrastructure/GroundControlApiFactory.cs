using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using MongoDB.Driver;
using Xunit;

namespace GroundControl.Api.Tests.Infrastructure;

public sealed class GroundControlApiFactory : WebApplicationFactory<Program>
{
    private readonly IMongoDatabase _database;
    private readonly MongoFixture _mongoFixture;

    public GroundControlApiFactory(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture ?? throw new ArgumentNullException(nameof(mongoFixture));
        _database = _mongoFixture.CreateDatabase();
    }

    public IMongoDatabase Database => _database;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _mongoFixture.ConnectionString,
                ["Persistence:MongoDb:DatabaseName"] = _database.DatabaseNamespace.DatabaseName,
                ["GroundControl:Security:AuthenticationMode"] = "None"
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging(c => c.OutputSink = message => TestContext.Current.TestOutputHelper?.WriteLine(message));
            logging.AddFilter<FakeLoggerProvider>(l => l >= LogLevel.Debug);
        });
    }
}