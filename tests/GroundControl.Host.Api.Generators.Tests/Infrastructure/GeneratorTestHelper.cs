using System.Collections.Immutable;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// Runs the <see cref="WebApiModuleGenerator"/> on the provided compilation and returns the driver
    /// for snapshot verification with Verify.
    /// </summary>
    public static GeneratorDriver CreateDriver(CSharpCompilation compilation)
    {
        var generator = new WebApiModuleGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(ParseOptions);

        return driver.RunGenerators(compilation);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        // Include the core runtime and ASP.NET Core types needed by the generated code
        var assemblies = new[]
        {
            typeof(object).Assembly, // System.Runtime / mscorlib
            typeof(Attribute).Assembly, // System.Runtime
            typeof(Console).Assembly, // System.Console
            typeof(WebApplication).Assembly,
            typeof(WebApplicationBuilder).Assembly,
            typeof(IConfiguration).Assembly,
            typeof(ConfigurationExtensions).Assembly,
            typeof(IServiceCollection).Assembly,
            typeof(IValidateOptions<>).Assembly,
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