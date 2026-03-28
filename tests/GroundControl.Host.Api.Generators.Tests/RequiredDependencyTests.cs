namespace GroundControl.Host.Api.Generators.Tests;

public sealed class RequiredDependencyTests
{
    [Fact]
    public void RequiredDependency_CheckEmitted()
    {
        // Arrange — B has a required dependency on A
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleA>(Required = true)]
            internal sealed class ModuleB : IWebApiModule
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
        bootstrapSource.ShouldContain("requires");
        bootstrapSource.ShouldContain("IsModuleEnabled(builder.Configuration, \"ModuleA\")");
        bootstrapSource.ShouldContain("throw new global::System.InvalidOperationException(");
    }

    [Fact]
    public void SoftDependency_NoCheck()
    {
        // Arrange — B has a soft (non-required) dependency on A
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleA>]
            internal sealed class ModuleB : IWebApiModule
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

        // The moduleB section should NOT contain a throw for required dependency
        var moduleBSection = bootstrapSource[bootstrapSource.IndexOf("new global::ModuleB()", StringComparison.Ordinal)..];
        moduleBSection.ShouldNotContain("throw new global::System.InvalidOperationException(");
    }
}