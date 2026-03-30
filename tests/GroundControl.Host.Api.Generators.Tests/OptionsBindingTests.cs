namespace GroundControl.Host.Api.Generators.Tests;

public sealed class OptionsBindingTests
{
    [Fact]
    public Task OptionsModule_ConstructorInjection()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;

            public class MyOptions
            {
                public string Value { get; set; } = "";
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
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
    public Task OptionsModule_ConfigurationKeyOverride()
    {
        // Arrange — TOptions has [ConfigurationKey("CustomKey")]
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("CustomKey")]
            public class MyOptions
            {
                public string Value { get; set; } = "";
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
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
    public Task OptionsModule_StripOptionsSuffix()
    {
        // Arrange — FooOptions → section name "Foo"
        var source = """
            using GroundControl.Host.Api;

            public class FooOptions
            {
                public string Value { get; set; } = "";
            }

            internal sealed class FooModule : IWebApiModule<FooOptions>
            {
                public FooModule(FooOptions options) { }
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
    public Task OptionsModule_StripOptionSuffix()
    {
        // Arrange — FooOption → section name "Foo"
        var source = """
            using GroundControl.Host.Api;

            public class FooOption
            {
                public string Value { get; set; } = "";
            }

            internal sealed class FooModule : IWebApiModule<FooOption>
            {
                public FooModule(FooOption options) { }
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
    public Task OptionsModule_NoSuffix_UsesFullName()
    {
        // Arrange — FooConfig (no Options/Option suffix) → section name "FooConfig"
        var source = """
            using GroundControl.Host.Api;

            public class FooConfig
            {
                public string Value { get; set; } = "";
            }

            internal sealed class FooModule : IWebApiModule<FooConfig>
            {
                public FooModule(FooConfig options) { }
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
    public Task OptionsModule_EmptyAfterStrip_UsesFullName()
    {
        // Arrange — "Options" type name → stripping "Options" leaves empty, so use "Options"
        var source = """
            using GroundControl.Host.Api;

            public class Options
            {
                public string Value { get; set; } = "";
            }

            internal sealed class FooModule : IWebApiModule<Options>
            {
                public FooModule(Options options) { }
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
    public Task OptionsModule_PropertyConfigurationKeyOverride()
    {
        // Arrange — class-level and property-level [ConfigurationKey]
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("Authentication")]
            public class SecurityOptions
            {
                [ConfigurationKey("ConnectionStrings:Storage")]
                public string Storage { get; set; } = "";

                public string Mode { get; set; } = "None";
            }

            internal sealed class SecurityModule : IWebApiModule<SecurityOptions>
            {
                public SecurityModule(SecurityOptions options) { }
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
    public Task OptionsModule_MultiplePropertyOverrides()
    {
        // Arrange — multiple properties with [ConfigurationKey]
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("App")]
            public class AppOptions
            {
                [ConfigurationKey("ConnectionStrings:Database")]
                public string Database { get; set; } = "";

                [ConfigurationKey("ConnectionStrings:Cache")]
                public string Cache { get; set; } = "";

                public string Name { get; set; } = "";
            }

            internal sealed class AppModule : IWebApiModule<AppOptions>
            {
                public AppModule(AppOptions options) { }
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
    public Task OptionsModule_PropertyOverride_ValueType()
    {
        // Arrange — property override on a value type (int)
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("App")]
            public class AppOptions
            {
                [ConfigurationKey("Limits:MaxRetries")]
                public int MaxRetries { get; set; } = 3;

                public string Name { get; set; } = "";
            }

            internal sealed class AppModule : IWebApiModule<AppOptions>
            {
                public AppModule(AppOptions options) { }
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
    public Task OptionsModule_PropertyFallbackKey()
    {
        // Arrange — property with [ConfigurationKey(IsFallback = true)] uses fallback binding
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("Authentication")]
            public class SecurityOptions
            {
                [ConfigurationKey("ConnectionStrings:Storage", IsFallback = true)]
                public string Storage { get; set; } = "";

                public string Mode { get; set; } = "None";
            }

            internal sealed class SecurityModule : IWebApiModule<SecurityOptions>
            {
                public SecurityModule(SecurityOptions options) { }
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
    public Task OptionsModule_MixedOverrideAndFallback()
    {
        // Arrange — one override and one fallback property in the same class
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("App")]
            public class AppOptions
            {
                [ConfigurationKey("ConnectionStrings:Database")]
                public string Database { get; set; } = "";

                [ConfigurationKey("ConnectionStrings:Cache", IsFallback = true)]
                public string Cache { get; set; } = "";

                public string Name { get; set; } = "";
            }

            internal sealed class AppModule : IWebApiModule<AppOptions>
            {
                public AppModule(AppOptions options) { }
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
    public Task OptionsModule_PropertyFallbackKey_ValueType()
    {
        // Arrange — fallback on a value type (int) property
        var source = """
            using GroundControl.Host.Api;

            [ConfigurationKey("App")]
            public class AppOptions
            {
                [ConfigurationKey("Limits:MaxRetries", IsFallback = true)]
                public int MaxRetries { get; set; } = 3;

                public string Name { get; set; } = "";
            }

            internal sealed class AppModule : IWebApiModule<AppOptions>
            {
                public AppModule(AppOptions options) { }
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
    public Task OptionsModule_PropertyFallbackKey_ConventionSectionName()
    {
        // Arrange — fallback without class-level [ConfigurationKey], uses convention-based section name
        var source = """
            using GroundControl.Host.Api;

            public class FooOptions
            {
                [ConfigurationKey("Defaults:ApiKey", IsFallback = true)]
                public string ApiKey { get; set; } = "";

                public string Name { get; set; } = "";
            }

            internal sealed class FooModule : IWebApiModule<FooOptions>
            {
                public FooModule(FooOptions options) { }
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