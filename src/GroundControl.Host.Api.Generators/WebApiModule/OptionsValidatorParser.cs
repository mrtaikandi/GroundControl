using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static GroundControl.Host.Api.Generators.Internals.KnownTypes;

namespace GroundControl.Host.Api.Generators.WebApiModule;

internal static class OptionsValidatorParser
{
    internal static OptionsValidatorDescriptor? TransformToDescriptor(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol || symbol.IsAbstract)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;
        var validateOptionsInterface = compilation.GetTypeByMetadataName(ValidateOptionsInterfaceMetadataName);

        if (validateOptionsInterface is null)
        {
            return null;
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, validateOptionsInterface))
            {
                continue;
            }

            var optionsType = iface.TypeArguments[0];
            return new OptionsValidatorDescriptor(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                optionsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }
}