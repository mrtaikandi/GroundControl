using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Features.Clients;
using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
            var authMode = context.Configuration.GetValue<AuthenticationMode>("GroundControl:Security:AuthenticationMode");
            if (authMode == AuthenticationMode.BuiltIn)
            {
                var gcOptions = context.Configuration.GetSection(GroundControlOptions.SectionName).Get<GroundControlOptions>()!;
                new BuiltInAuthConfigurator(gcOptions).ConfigureServices(services, context.Configuration);
            }

            // Remove background services that never trigger during tests to reduce per-factory startup cost
            Type[] unnecessaryServices = [typeof(ClientCleanupService), typeof(SnapshotCacheInvalidator)];
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(IHostedService) && unnecessaryServices.Contains(d.ImplementationType))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
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