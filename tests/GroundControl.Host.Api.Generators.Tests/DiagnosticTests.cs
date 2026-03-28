namespace GroundControl.Host.Api.Generators.Tests;

public sealed class DiagnosticTests
{
    [Fact]
    public void CircularDependency_GCA001()
    {
        // Arrange — A → B → A
        var source = """
            using GroundControl.Host.Api;

            [RunsAfter<ModuleB>]
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
        var diagnostics = result.GetDiagnostics("GCA001");
        diagnostics.Length.ShouldBe(2);
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void SelfReference_GCA001()
    {
        // Arrange — A references itself
        var source = """
            using GroundControl.Host.Api;

            [RunsAfter<SelfModule>]
            internal sealed class SelfModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        var diagnostics = result.GetDiagnostics("GCA001");
        diagnostics.Length.ShouldBe(1);
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void WrongConstructor_PlainModule_GCA002()
    {
        // Arrange — plain IWebApiModule with a required constructor parameter
        var source = """
            using GroundControl.Host.Api;

            internal sealed class BadModule : IWebApiModule
            {
                public BadModule(string name) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        var diagnostics = result.GetDiagnostics("GCA002");
        diagnostics.Length.ShouldBe(1);
        diagnostics[0].GetMessage().ShouldContain("parameterless constructor");
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void WrongConstructor_OptionsModule_GCA002()
    {
        // Arrange — IWebApiModule<TOptions> without a constructor accepting TOptions
        var source = """
            using GroundControl.Host.Api;

            public class MyOptions
            {
                public string Value { get; set; } = "";
            }

            internal sealed class BadOptionsModule : IWebApiModule<MyOptions>
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        var diagnostics = result.GetDiagnostics("GCA002");
        diagnostics.Length.ShouldBe(1);
        diagnostics[0].GetMessage().ShouldContain("MyOptions");
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void DuplicateRunsAfter_GCA003()
    {
        // Arrange — two [RunsAfter<ModuleA>] on same class
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleA>]
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
        var diagnostics = result.GetDiagnostics("GCA003");
        diagnostics.Length.ShouldBe(1);
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void DuplicateRunsBefore_GCA003()
    {
        // Arrange — two [RunsBefore<ModuleB>] on same class
        var source = """
            using GroundControl.Host.Api;

            [RunsBefore<ModuleB>]
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
        var diagnostics = result.GetDiagnostics("GCA003");
        diagnostics.Length.ShouldBe(1);
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void CrossDuplicate_GCA003()
    {
        // Arrange — [RunsAfter<ModuleA>] and [RunsBefore<ModuleA>] on same class targeting same module
        var source = """
            using GroundControl.Host.Api;

            internal sealed class ModuleA : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<ModuleA>]
            [RunsBefore<ModuleA>]
            internal sealed class ModuleB : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        var diagnostics = result.GetDiagnostics("GCA003");
        diagnostics.Length.ShouldBe(1);
        result.HasBootstrapSource.ShouldBeFalse();
    }

    [Fact]
    public void TargetNotFound_GCA004()
    {
        // Arrange — RunsAfter referencing a class that doesn't implement IWebApiModule
        var source = """
            using GroundControl.Host.Api;

            internal sealed class NotAModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            [RunsAfter<NotDiscoveredModule>]
            internal sealed class MyModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }

            internal sealed class NotDiscoveredModule
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.CreateAndRun(source);

        // Assert
        var diagnostics = result.GetDiagnostics("GCA004");
        diagnostics.Length.ShouldBe(1);
        diagnostics[0].GetMessage().ShouldContain("MyModule");
        result.HasBootstrapSource.ShouldBeFalse();
    }
}