namespace GroundControl.Host.Api.Generators.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public Task RealisticGraph_CorrectOrder()
    {
        // Arrange — 6 modules with a realistic dependency graph:
        //   Logging (root, no deps)
        //   Config (root, no deps)
        //   Database [RunsAfter<Config>]
        //   Auth [RunsAfter<Config>, RunsAfter<Logging>]
        //   Api [RunsAfter<Database>, RunsAfter<Auth>]
        //   HealthChecks [RunsAfter<Database>, RunsBefore<Api>]
        var source = """
            using GroundControl.Host.Api;

            internal sealed class LoggingModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class ConfigModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ConfigModule>]
            internal sealed class DatabaseModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ConfigModule>]
            [RunsAfter<LoggingModule>]
            internal sealed class AuthModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<DatabaseModule>]
            [RunsAfter<AuthModule>]
            internal sealed class ApiModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<DatabaseModule>]
            [RunsBefore<ApiModule>]
            internal sealed class HealthChecksModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task MixedModules_WithAndWithoutOptions()
    {
        // Arrange — mix of plain and options modules
        var source = """
            using GroundControl.Host.Api;

            public class AuthOptions
            {
                public string Issuer { get; set; } = "";
            }

            public class CacheConfig
            {
                public int TimeoutSeconds { get; set; }
            }

            internal sealed class LoggingModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<LoggingModule>]
            internal sealed class AuthModule : IWebApiModule<AuthOptions>
            {
                public AuthModule(AuthOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<LoggingModule>]
            internal sealed class CacheModule : IWebApiModule<CacheConfig>
            {
                public CacheModule(CacheConfig options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<AuthModule>]
            [RunsAfter<CacheModule>]
            internal sealed class ApiModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task FullFeatureGraph_AllDiagnosticsClean()
    {
        // Arrange — complex graph with all feature types, no errors expected
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("Telemetry")]
            public class ObservabilityOptions
            {
                public bool Enabled { get; set; } = true;
            }

            public class DatabaseOptions
            {
                public string ConnectionString { get; set; } = "";
            }

            internal sealed class CoreModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<CoreModule>]
            internal sealed class ObservabilityModule : IWebApiModule<ObservabilityOptions>
            {
                public ObservabilityModule(ObservabilityOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<CoreModule>]
            internal sealed class DatabaseModule : IWebApiModule<DatabaseOptions>
            {
                public DatabaseModule(DatabaseOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<DatabaseModule>(Required = true)]
            [RunsAfter<ObservabilityModule>]
            internal sealed class ApiModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsBefore<ApiModule>]
            [RunsAfter<DatabaseModule>]
            internal sealed class MigrationModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }
}