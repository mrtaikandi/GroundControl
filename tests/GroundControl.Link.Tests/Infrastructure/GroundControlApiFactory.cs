using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using MongoDB.Driver;
using Xunit;
using ApiAuthenticationMode = GroundControl.Api.Shared.Security.AuthenticationMode;
using ApiGroundControlOptions = GroundControl.Api.Shared.Configuration.GroundControlOptions;
using BuiltInAuthConfigurator = GroundControl.Api.Shared.Security.Auth.BuiltInAuthConfigurator;

namespace GroundControl.Link.Tests.Infrastructure;

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
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _mongoFixture.ConnectionString,
                ["Persistence:MongoDb:DatabaseName"] = _database.DatabaseNamespace.DatabaseName,
                ["GroundControl:Security:AuthenticationMode"] = "None",
            };

            foreach (var kvp in _extraConfig)
            {
                config[kvp.Key] = kvp.Value;
            }

            configurationBuilder.AddInMemoryCollection(config);
        });

        // WebApplicationFactory applies config overrides AFTER Program.cs eagerly reads
        // GroundControlOptions, so auth mode selection in Program.cs always sees the default.
        // Re-apply auth services here when the test config specifies a non-default mode.
        builder.ConfigureServices((context, services) =>
        {
            var authMode = context.Configuration.GetValue<ApiAuthenticationMode>("GroundControl:Security:AuthenticationMode");
            if (authMode == ApiAuthenticationMode.BuiltIn)
            {
                var gcOptions = context.Configuration.GetSection(ApiGroundControlOptions.SectionName).Get<ApiGroundControlOptions>()!;
                new BuiltInAuthConfigurator(gcOptions).ConfigureServices(services, context.Configuration);
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFakeLogging(c => c.OutputSink = message => TestContext.Current.TestOutputHelper?.WriteLine(message));
            logging.AddFilter<FakeLoggerProvider>(l => l >= LogLevel.Debug);
        });
    }
}