using System.Collections.Immutable;
using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static GroundControl.Host.Api.Generators.Internals.KnownTypes;

namespace GroundControl.Host.Api.Generators.WebApiModule;

internal static class WebApiModuleParser
{
    internal static ModuleResult? TransformToModuleResult(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol || symbol.IsAbstract)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;

        var moduleInterface = compilation.GetTypeByMetadataName(ModuleInterfaceMetadataName);
        if (moduleInterface is null)
        {
            return null;
        }

        if (!symbol.AllInterfaces.Contains(moduleInterface, SymbolEqualityComparer.Default))
        {
            return null;
        }

        // Determine if it implements IWebApiModule<TOptions> and extract TOptions
        var genericModuleInterface = compilation.GetTypeByMetadataName(GenericModuleInterfaceMetadataName);
        INamedTypeSymbol? optionsType = null;

        if (genericModuleInterface is not null)
        {
            foreach (var iface in symbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, genericModuleInterface))
                {
                    optionsType = iface.TypeArguments[0] as INamedTypeSymbol;
                    break;
                }
            }
        }

        // Validate constructor
        string? constructorError = null;

        if (optionsType is not null)
        {
            var hasValidCtor = symbol.InstanceConstructors.Any(ctor =>
                ctor.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, optionsType));

            if (!hasValidCtor)
            {
                constructorError = "Module '" + symbol.Name + "' must have a constructor accepting '" +
                                   optionsType.ToDisplayString() + "'";
            }
        }
        else
        {
            var hasParameterlessCtor = symbol.InstanceConstructors.Any(ctor => ctor.Parameters.Length == 0);

            if (!hasParameterlessCtor)
            {
                constructorError = "Module '" + symbol.Name + "' must have a parameterless constructor";
            }
        }

        // Extract RunsAfter and RunsBefore attributes
        var runsAfterBuilder = ImmutableArray.CreateBuilder<ModuleDependencyDescriptor>();
        var runsBeforeBuilder = ImmutableArray.CreateBuilder<string>();

        var runsAfterAttrType = compilation.GetTypeByMetadataName(RunsAfterAttributeMetadataName);
        var runsBeforeAttrType = compilation.GetTypeByMetadataName(RunsBeforeAttributeMetadataName);

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass is null)
            {
                continue;
            }

            if (runsAfterAttrType is not null &&
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass.OriginalDefinition, runsAfterAttrType))
            {
                var targetType = attr.AttributeClass.TypeArguments[0];
                var targetFqn = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var required = false;
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Required" && namedArg.Value.Value is bool r)
                    {
                        required = r;
                        break;
                    }
                }

                runsAfterBuilder.Add(new ModuleDependencyDescriptor(targetFqn, required));
            }
            else if (runsBeforeAttrType is not null &&
                     SymbolEqualityComparer.Default.Equals(attr.AttributeClass.OriginalDefinition, runsBeforeAttrType))
            {
                var targetType = attr.AttributeClass.TypeArguments[0];
                var targetFqn = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                runsBeforeBuilder.Add(targetFqn);
            }
        }

        // Resolve options info
        string? optionsFqn = null;
        string? optionsTypeName = null;
        string? configSectionName = null;

        if (optionsType is not null)
        {
            optionsFqn = optionsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            optionsTypeName = optionsType.Name;
            configSectionName = ResolveSectionName(optionsType, compilation);
        }

        var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var identifierLocation = classDecl.Identifier.GetLocation();
        var lineSpan = identifierLocation.GetLineSpan();

        var moduleInfo = new ModuleDescriptor(
            FullyQualifiedName: fqn,
            TypeName: symbol.Name,
            RunsAfter: runsAfterBuilder.ToImmutable(),
            RunsBefore: runsBeforeBuilder.ToImmutable(),
            LocationFilePath: lineSpan.Path,
            LocationSpan: identifierLocation.SourceSpan,
            LocationLineSpan: lineSpan.Span,
            OptionsTypeFullyQualifiedName: optionsFqn,
            OptionsTypeName: optionsTypeName,
            ConfigurationSectionName: configSectionName);

        return new ModuleResult(moduleInfo, constructorError);
    }

    private static string ResolveSectionName(INamedTypeSymbol optionsType, Compilation compilation)
    {
        var configKeyAttr = compilation.GetTypeByMetadataName(ConfigurationKeyAttributeMetadataName);

        if (configKeyAttr is not null)
        {
            foreach (var attr in optionsType.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, configKeyAttr) &&
                    attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is string key)
                {
                    return key;
                }
            }
        }

        var name = optionsType.Name;

        if (name.Length > 7 && name.EndsWith("Options"))
        {
            return name.Substring(0, name.Length - 7);
        }

        if (name.Length > 6 && name.EndsWith("Option"))
        {
            return name.Substring(0, name.Length - 6);
        }

        return name;
    }
}