namespace GroundControl.Host.Api.Generators.Tests;

public sealed class EnabledDisabledTests
{
    [Fact]
    public void ModuleEnabled_Check()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;

            internal sealed class MyModule : IWebApiModule
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
        bootstrapSource.ShouldContain("IsModuleEnabled(builder.Configuration, \"My\")");
    }

    [Fact]
    public void ModuleNameStripsModuleSuffix()
    {
        // Arrange — FooModule should use config path "Foo"
        var source = """
            using GroundControl.Host.Api;

            internal sealed class FooModule : IWebApiModule
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
        bootstrapSource.ShouldContain("IsModuleEnabled(builder.Configuration, \"Foo\")");
    }

    [Fact]
    public void ModuleNameWithoutSuffix_UsesFullName()
    {
        // Arrange — FooSetup (no "Module" suffix) should use config path "FooSetup"
        var source = """
            using GroundControl.Host.Api;

            internal sealed class FooSetup : IWebApiModule
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
        bootstrapSource.ShouldContain("IsModuleEnabled(builder.Configuration, \"FooSetup\")");
    }
}