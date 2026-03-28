namespace GroundControl.Host.Api.Generators.Tests;

public sealed class DiscoveryTests
{
    [Fact]
    public void SingleModule_Discovered()
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
        result.HasBootstrapSource.ShouldBeTrue();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain("new global::MyModule()");
    }

    [Fact]
    public void AbstractModule_Ignored()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;

            internal abstract class AbstractModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void NoModules_NoOutput()
    {
        // Arrange
        var source = """
            internal sealed class NotAModule
            {
                public void DoStuff() { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void ModuleWithoutBaseList_Ignored()
    {
        // Arrange — class has no base list at all (no : IWebApiModule)
        var source = """
            internal sealed class PlainClass
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.HasBootstrapSource.ShouldBeFalse();
    }
}