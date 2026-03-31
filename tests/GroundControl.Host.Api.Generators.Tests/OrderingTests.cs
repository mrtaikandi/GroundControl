namespace GroundControl.Host.Api.Generators.Tests;

public sealed class OrderingTests
{
    [Fact]
    public Task RunsAfter_OrderRespected()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task RunsBefore_OrderRespected()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task AlphabeticalTieBreak()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task DiamondDependency_Handled()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task MultipleRoots_AlphabeticalOrder()
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
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task IndependentModules_RunAfterGraphParticipants()
    {
        // Arrange — AaaModule and BbbModule have no ordering attrs and are not referenced,
        // so they should run after CoreModule and ServiceModule despite sorting alphabetically first
        var source = """
            using GroundControl.Host.Api;

            internal sealed class AaaModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class BbbModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class CoreModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<CoreModule>]
            internal sealed class ServiceModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert — expected order: CoreModule, ServiceModule, AaaModule, BbbModule
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task IndependentModules_NotAffectedByAlphabeticalPosition()
    {
        // Arrange — AaaModule (independent) would sort first alphabetically,
        // but should still run after ZzzModule (participant) and MiddleModule (participant)
        var source = """
            using GroundControl.Host.Api;

            internal sealed class AaaModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class MiddleModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<MiddleModule>]
            internal sealed class ZzzModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert — expected order: MiddleModule, ZzzModule, AaaModule
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task CycleDetection_WithIndependentModules()
    {
        // Arrange — CycleA and CycleB form a cycle, IndependentModule has no ordering attrs.
        // The cycle should still be detected as GCA001.
        var source = """
            using GroundControl.Host.Api;

            internal sealed class IndependentModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<CycleB>]
            internal sealed class CycleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<CycleA>]
            internal sealed class CycleB : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert — GCA001 cycle diagnostic for CycleA and CycleB
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }
}