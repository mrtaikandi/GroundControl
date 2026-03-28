namespace GroundControl.Host.Api.Generators.Tests;

public sealed class OptionsBindingTests
{
    [Fact]
    public void OptionsModule_ConstructorInjection()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(".GetSection(\"My\")");
        bootstrapSource.ShouldContain(".Get<global::MyOptions>()");
        bootstrapSource.ShouldContain("new global::MyModule(myModuleOptions)");
    }

    [Fact]
    public void OptionsModule_ConfigurationKeyOverride()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(".GetSection(\"CustomKey\")");
    }

    [Fact]
    public void OptionsModule_StripOptionsSuffix()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(".GetSection(\"Foo\")");
    }

    [Fact]
    public void OptionsModule_StripOptionSuffix()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(".GetSection(\"Foo\")");
    }

    [Fact]
    public void OptionsModule_NoSuffix_UsesFullName()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(".GetSection(\"FooConfig\")");
    }

    [Fact]
    public void OptionsModule_EmptyAfterStrip_UsesFullName()
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
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        // "Options" has length 7, which equals 7 (not > 7), so the suffix isn't stripped
        bootstrapSource.ShouldContain(".GetSection(\"Options\")");
    }
}