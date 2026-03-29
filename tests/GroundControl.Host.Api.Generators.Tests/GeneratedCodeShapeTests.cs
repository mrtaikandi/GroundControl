namespace GroundControl.Host.Api.Generators.Tests;

public sealed class GeneratedCodeShapeTests
{
    [Fact]
    public Task SingleModule_GeneratedCodeShape()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task RootNamespace_FromAssemblyName()
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
        var driver = GeneratorTestHelper.CreateDriver(
            GeneratorTestHelper.CreateCompilation(source, assemblyName: "Foo.Bar"));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }
}