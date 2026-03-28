namespace GroundControl.Host.Api.Generators.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public void RealisticGraph_CorrectOrder()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;

        var positions = new Dictionary<string, int>
        {
            ["Config"] = bootstrapSource.IndexOf("new global::ConfigModule()", StringComparison.Ordinal),
            ["Logging"] = bootstrapSource.IndexOf("new global::LoggingModule()", StringComparison.Ordinal),
            ["Database"] = bootstrapSource.IndexOf("new global::DatabaseModule()", StringComparison.Ordinal),
            ["Auth"] = bootstrapSource.IndexOf("new global::AuthModule()", StringComparison.Ordinal),
            ["Api"] = bootstrapSource.IndexOf("new global::ApiModule()", StringComparison.Ordinal),
            ["HealthChecks"] = bootstrapSource.IndexOf("new global::HealthChecksModule()", StringComparison.Ordinal),
        };

        // All modules should be present
        foreach (var (name, pos) in positions)
        {
            pos.ShouldBeGreaterThan(-1, $"Module {name} should be present in generated code");
        }

        // Config before Database, Auth
        positions["Config"].ShouldBeLessThan(positions["Database"]);
        positions["Config"].ShouldBeLessThan(positions["Auth"]);

        // Logging before Auth
        positions["Logging"].ShouldBeLessThan(positions["Auth"]);

        // Database before Api and HealthChecks
        positions["Database"].ShouldBeLessThan(positions["Api"]);
        positions["Database"].ShouldBeLessThan(positions["HealthChecks"]);

        // Auth before Api
        positions["Auth"].ShouldBeLessThan(positions["Api"]);

        // HealthChecks before Api (via RunsBefore)
        positions["HealthChecks"].ShouldBeLessThan(positions["Api"]);
    }

    [Fact]
    public void MixedModules_WithAndWithoutOptions()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;

        // Plain module: simple instantiation
        bootstrapSource.ShouldContain("new global::LoggingModule()");
        bootstrapSource.ShouldContain("new global::ApiModule()");

        // Options module: config binding and constructor injection
        bootstrapSource.ShouldContain(".GetSection(\"Auth\")");
        bootstrapSource.ShouldContain(".Get<global::AuthOptions>()");
        bootstrapSource.ShouldContain("new global::AuthModule(authModuleOptions)");

        // CacheConfig has no suffix to strip
        bootstrapSource.ShouldContain(".GetSection(\"CacheConfig\")");
        bootstrapSource.ShouldContain(".Get<global::CacheConfig>()");
        bootstrapSource.ShouldContain("new global::CacheModule(cacheModuleOptions)");
    }

    [Fact]
    public void FullFeatureGraph_AllDiagnosticsClean()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.HasBootstrapSource.ShouldBeTrue();

        var bootstrapSource = result.GetBootstrapSource()!;

        // ConfigurationKey override is respected
        bootstrapSource.ShouldContain(".GetSection(\"Telemetry\")");

        // DatabaseOptions uses suffix stripping
        bootstrapSource.ShouldContain(".GetSection(\"Database\")");

        // Required dependency check is present for Api → Database
        bootstrapSource.ShouldContain("requires 'DatabaseModule' to be enabled");

        // Verify it returns WebApplication
        bootstrapSource.ShouldContain("return app;");

        // Verify all modules appear
        bootstrapSource.ShouldContain("new global::CoreModule()");
        bootstrapSource.ShouldContain("new global::ObservabilityModule(");
        bootstrapSource.ShouldContain("new global::DatabaseModule(");
        bootstrapSource.ShouldContain("new global::ApiModule()");
        bootstrapSource.ShouldContain("new global::MigrationModule()");
    }
}