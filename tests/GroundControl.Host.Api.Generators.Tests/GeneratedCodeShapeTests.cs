namespace GroundControl.Host.Api.Generators.Tests;

public sealed class GeneratedCodeShapeTests
{
    private const string SingleModuleSource = """
        using GroundControl.Host.Api;

        internal sealed class MyModule : IWebApiModule
        {
            public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
            public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
        }
        """;

    [Fact]
    public void BuildWebApiModules_ReturnsWebApplication()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.CreateAndRun(SingleModuleSource);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain(
            "public static global::Microsoft.AspNetCore.Builder.WebApplication BuildWebApiModules(");
    }

    [Fact]
    public void BuilderBuild_BetweenPhases()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.CreateAndRun(SingleModuleSource);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        var servicePhaseEnd = bootstrapSource.IndexOf("var app = builder.Build();", StringComparison.Ordinal);
        var appPhase = bootstrapSource.IndexOf("?.OnApplicationConfiguration(app);", StringComparison.Ordinal);

        servicePhaseEnd.ShouldBeGreaterThan(-1);
        appPhase.ShouldBeGreaterThan(-1);
        servicePhaseEnd.ShouldBeLessThan(appPhase);

        // OnServiceConfiguration appears before builder.Build()
        var serviceConfigCall = bootstrapSource.IndexOf(".OnServiceConfiguration(builder);", StringComparison.Ordinal);
        serviceConfigCall.ShouldBeLessThan(servicePhaseEnd);
    }

    [Fact]
    public void NullableLocal_ForDisabledSupport()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.CreateAndRun(SingleModuleSource);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain("global::MyModule? myModule = null;");
    }

    [Fact]
    public void NullConditional_AppPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.CreateAndRun(SingleModuleSource);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain("myModule?.OnApplicationConfiguration(app);");
    }

    [Fact]
    public void RootNamespace_FromAssemblyName()
    {
        // Arrange
        var result = GeneratorTestHelper.CreateAndRun(SingleModuleSource, assemblyName: "Foo.Bar");

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var bootstrapSource = result.GetBootstrapSource()!;
        bootstrapSource.ShouldContain("namespace Foo.Bar;");
    }
}