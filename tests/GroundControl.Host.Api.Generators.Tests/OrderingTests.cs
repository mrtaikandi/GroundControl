namespace GroundControl.Host.Api.Generators.Tests;

public sealed class OrderingTests
{
    [Fact]
    public void RunsAfter_OrderRespected()
    {
        // Arrange — B runs after A, so A should appear first
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
        var indexA = bootstrapSource.IndexOf("new global::ModuleA()", StringComparison.Ordinal);
        var indexB = bootstrapSource.IndexOf("new global::ModuleB()", StringComparison.Ordinal);
        indexA.ShouldBeGreaterThan(-1);
        indexB.ShouldBeGreaterThan(-1);
        indexA.ShouldBeLessThan(indexB);
    }

    [Fact]
    public void RunsBefore_OrderRespected()
    {
        // Arrange — A runs before B, so A should appear first
        var source = """
            using GroundControl.Host.Api;

            [RunsBefore<ModuleB>]
            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

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
        var indexA = bootstrapSource.IndexOf("new global::ModuleA()", StringComparison.Ordinal);
        var indexB = bootstrapSource.IndexOf("new global::ModuleB()", StringComparison.Ordinal);
        indexA.ShouldBeGreaterThan(-1);
        indexB.ShouldBeGreaterThan(-1);
        indexA.ShouldBeLessThan(indexB);
    }

    [Fact]
    public void AlphabeticalTieBreak()
    {
        // Arrange — two independent modules, should appear in alphabetical order by FQN
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ZebraModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class AlphaModule : IWebApiModule
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
        var indexAlpha = bootstrapSource.IndexOf("new global::AlphaModule()", StringComparison.Ordinal);
        var indexZebra = bootstrapSource.IndexOf("new global::ZebraModule()", StringComparison.Ordinal);
        indexAlpha.ShouldBeGreaterThan(-1);
        indexZebra.ShouldBeGreaterThan(-1);
        indexAlpha.ShouldBeLessThan(indexZebra);
    }

    [Fact]
    public void DiamondDependency_Handled()
    {
        // Arrange — Diamond: D has no deps, B→D, C→D, A→B, A→C
        // Expected order: D, then B and C (alphabetical), then A
        var source = """
            using GroundControl.Host.Api;

            [RunsAfter<ModuleB>]
            [RunsAfter<ModuleC>]
            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleD>]
            internal sealed class ModuleB : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleD>]
            internal sealed class ModuleC : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class ModuleD : IWebApiModule
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
        var indexA = bootstrapSource.IndexOf("new global::ModuleA()", StringComparison.Ordinal);
        var indexB = bootstrapSource.IndexOf("new global::ModuleB()", StringComparison.Ordinal);
        var indexC = bootstrapSource.IndexOf("new global::ModuleC()", StringComparison.Ordinal);
        var indexD = bootstrapSource.IndexOf("new global::ModuleD()", StringComparison.Ordinal);

        // D before B and C
        indexD.ShouldBeLessThan(indexB);
        indexD.ShouldBeLessThan(indexC);
        // B and C before A
        indexB.ShouldBeLessThan(indexA);
        indexC.ShouldBeLessThan(indexA);
        // B before C (alphabetical tie-break)
        indexB.ShouldBeLessThan(indexC);
    }

    [Fact]
    public void MultipleRoots_AlphabeticalOrder()
    {
        // Arrange — two independent modules
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ModuleB : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class ModuleA : IWebApiModule
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
        var indexA = bootstrapSource.IndexOf("new global::ModuleA()", StringComparison.Ordinal);
        var indexB = bootstrapSource.IndexOf("new global::ModuleB()", StringComparison.Ordinal);
        indexA.ShouldBeLessThan(indexB);
    }
}