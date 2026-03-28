using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace GroundControl.Host.Api.Generators.Tests.Infrastructure;

internal static class GeneratorTestHelper
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp13);

    /// <summary>
    /// Creates a <see cref="CSharpCompilation"/> with the provided source code and all necessary
    /// ASP.NET Core assembly references for the generator to discover module interfaces and attributes.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions);

        var references = GetMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Runs the <see cref="WebApiModuleGenerator"/> on the provided compilation and returns the result.
    /// </summary>
    public static GeneratorRunResult RunGenerator(CSharpCompilation compilation)
    {
        var generator = new WebApiModuleGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(ParseOptions);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        return new GeneratorRunResult(outputCompilation, diagnostics, driver.GetRunResult());
    }

    /// <summary>
    /// Creates a compilation from the source and runs the generator in one step.
    /// </summary>
    public static GeneratorRunResult CreateAndRun(string source, string assemblyName = "TestAssembly")
    {
        var compilation = CreateCompilation(source, assemblyName);
        return RunGenerator(compilation);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        // Include the core runtime and ASP.NET Core types needed by the generated code
        var assemblies = new[]
        {
            typeof(object).Assembly, // System.Runtime / mscorlib
            typeof(Attribute).Assembly, // System.Runtime
            typeof(Console).Assembly, // System.Console
            typeof(Microsoft.AspNetCore.Builder.WebApplication).Assembly,
            typeof(Microsoft.AspNetCore.Builder.WebApplicationBuilder).Assembly,
            typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly,
            typeof(Microsoft.Extensions.Configuration.ConfigurationExtensions).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly,
        };

        var references = ImmutableArray.CreateBuilder<MetadataReference>();

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in assemblies)
        {
            if (seen.Add(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add System.Runtime for core type forwarding
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        foreach (var runtimeAssembly in new[] { "System.Runtime.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimeDir, runtimeAssembly);

            if (File.Exists(path) && seen.Add(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return references.ToImmutable();
    }
}

internal sealed record GeneratorRunResult(
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    GeneratorDriverRunResult DriverRunResult)
{
    /// <summary>
    /// Gets the generated source text for the bootstrap extensions file, or null if not emitted.
    /// </summary>
    public string? GetBootstrapSource()
    {
        var tree = OutputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("WebApiModuleExtensions.g.cs", StringComparison.Ordinal));

        return tree?.GetText().ToString();
    }

    /// <summary>
    /// Gets all diagnostics with the specified ID.
    /// </summary>
    public ImmutableArray<Diagnostic> GetDiagnostics(string id) =>
        DriverRunResult.Diagnostics
            .Where(d => d.Id == id)
            .ToImmutableArray();

    /// <summary>
    /// Gets whether the generator emitted the bootstrap extensions file.
    /// </summary>
    public bool HasBootstrapSource => GetBootstrapSource() is not null;
}