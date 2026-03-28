using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GroundControl.Host.Api.Generators;

[Generator]
public class WebApiModuleGenerator : IIncrementalGenerator
{
    private const string ModuleInterfaceMetadataName = "GroundControl.Host.Api.IWebApiModule";
    private const string GenericModuleInterfaceMetadataName = "GroundControl.Host.Api.IWebApiModule`1";
    private const string RunsAfterAttributeMetadataName = "GroundControl.Host.Api.RunsAfterAttribute`1";
    private const string RunsBeforeAttributeMetadataName = "GroundControl.Host.Api.RunsBeforeAttribute`1";
    private const string ConfigurationKeyAttributeMetadataName = "GroundControl.Host.Api.ConfigurationKeyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("IWebApiModule.g.cs", IWebApiModuleSource);
            ctx.AddSource("IWebApiModule{TOptions}.g.cs", IWebApiModuleOfTOptionsSource);
            ctx.AddSource("RunsAfterAttribute.g.cs", RunsAfterAttributeSource);
            ctx.AddSource("RunsBeforeAttribute.g.cs", RunsBeforeAttributeSource);
            ctx.AddSource("ConfigurationKeyAttribute.g.cs", ConfigurationKeyAttributeSource);
        });

        var moduleResults = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => TransformToModuleResult(ctx, ct))
            .Where(static r => r.HasValue)
            .Select(static (r, _) => r.GetValueOrDefault());

        var assemblyName = context.CompilationProvider.Select(
            static (c, _) => c.AssemblyName ?? "GeneratedNamespace");

        var combined = moduleResults.Collect().Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    private static ModuleResult? TransformToModuleResult(GeneratorSyntaxContext context, CancellationToken ct)
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
        var runsAfterBuilder = ImmutableArray.CreateBuilder<DependencyInfo>();
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

                runsAfterBuilder.Add(new DependencyInfo(targetFqn, required));
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

        var moduleInfo = new ModuleInfo(
            fullyQualifiedName: fqn,
            typeName: symbol.Name,
            runsAfter: runsAfterBuilder.ToImmutable(),
            runsBefore: runsBeforeBuilder.ToImmutable(),
            location: classDecl.Identifier.GetLocation(),
            optionsTypeFullyQualifiedName: optionsFqn,
            optionsTypeName: optionsTypeName,
            configurationSectionName: configSectionName);

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

    private static void Emit(SourceProductionContext context, ImmutableArray<ModuleResult> results, string rootNamespace)
    {
        if (results.IsDefaultOrEmpty)
        {
            return;
        }

        var validModules = new List<ModuleInfo>();
        var hasErrors = false;

        // Report GCA002 for constructor errors
        foreach (var result in results)
        {
            if (!result.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidConstructor,
                    result.Module.Location,
                    result.ConstructorErrorMessage));
                hasErrors = true;
            }
            else
            {
                validModules.Add(result.Module);
            }
        }

        if (hasErrors)
        {
            return;
        }

        // Validate GCA003 (duplicates) and GCA004 (missing targets)
        var allModuleNames = new HashSet<string>(validModules.Select(m => m.FullyQualifiedName));

        foreach (var module in validModules)
        {
            var allTargets = new HashSet<string>();

            foreach (var dep in module.RunsAfter)
            {
                if (!allTargets.Add(dep.TargetFullyQualifiedName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateDependency,
                        module.Location,
                        module.TypeName,
                        ForDisplay(dep.TargetFullyQualifiedName)));
                    hasErrors = true;
                }

                if (!allModuleNames.Contains(dep.TargetFullyQualifiedName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DependencyNotFound,
                        module.Location,
                        module.TypeName,
                        ForDisplay(dep.TargetFullyQualifiedName)));
                    hasErrors = true;
                }
            }

            foreach (var target in module.RunsBefore)
            {
                if (!allTargets.Add(target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateDependency,
                        module.Location,
                        module.TypeName,
                        ForDisplay(target)));
                    hasErrors = true;
                }

                if (!allModuleNames.Contains(target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DependencyNotFound,
                        module.Location,
                        module.TypeName,
                        ForDisplay(target)));
                    hasErrors = true;
                }
            }
        }

        if (hasErrors)
        {
            return;
        }

        // Topological sort
        var sortResult = TopologicalSorter.Sort(validModules.ToImmutableArray());

        if (sortResult.IsCycle)
        {
            var moduleLocationMap = validModules.ToDictionary(m => m.FullyQualifiedName);
            var participantNames = sortResult.CycleParticipants
                .Select(fqn => moduleLocationMap.TryGetValue(fqn, out var m) ? m.TypeName : ForDisplay(fqn))
                .ToList();
            var participantList = string.Join(", ", participantNames);

            foreach (var fqn in sortResult.CycleParticipants)
            {
                if (moduleLocationMap.TryGetValue(fqn, out var module))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.CircularDependency,
                        module.Location,
                        module.TypeName,
                        participantList));
                }
            }

            return;
        }

        EmitBootstrapSource(context, sortResult.SortedModules, rootNamespace);
    }

    private static void EmitBootstrapSource(
        SourceProductionContext context,
        ImmutableArray<ModuleInfo> sortedModules,
        string rootNamespace)
    {
        var moduleMap = sortedModules.ToDictionary(m => m.FullyQualifiedName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.Append("namespace ").Append(rootNamespace).AppendLine(";");
        sb.AppendLine();
        sb.AppendLine("internal static class WebApiModuleExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Builder.WebApplication BuildWebApiModules(");
        sb.AppendLine("        this global::Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)");
        sb.AppendLine("    {");

        // Service configuration phase
        for (var i = 0; i < sortedModules.Length; i++)
        {
            var module = sortedModules[i];
            var varName = ToCamelCase(module.TypeName);
            var moduleName = GetModuleName(module.TypeName);

            sb.Append("        ").Append(module.FullyQualifiedName).Append("? ").Append(varName).AppendLine(" = null;");
            sb.Append("        if (IsModuleEnabled(builder.Configuration, \"").Append(moduleName).AppendLine("\"))");
            sb.AppendLine("        {");

            // Required dependency checks
            foreach (var dep in module.RunsAfter)
            {
                if (dep.Required && moduleMap.TryGetValue(dep.TargetFullyQualifiedName, out var depModule))
                {
                    var depModuleName = GetModuleName(depModule.TypeName);
                    sb.Append("            if (!IsModuleEnabled(builder.Configuration, \"").Append(depModuleName).AppendLine("\"))");
                    sb.AppendLine("            {");
                    sb.Append("                throw new global::System.InvalidOperationException(").AppendLine();
                    sb.Append("                    \"Module '").Append(module.TypeName).Append("' requires '").Append(depModule.TypeName).AppendLine("' to be enabled.\");");
                    sb.AppendLine("            }");
                    sb.AppendLine();
                }
            }

            if (module.HasOptions)
            {
                sb.Append("            var ").Append(varName).AppendLine("Options = builder.Configuration");
                sb.Append("                .GetSection(\"").Append(module.ConfigurationSectionName).AppendLine("\")");
                sb.Append("                .Get<").Append(module.OptionsTypeFullyQualifiedName).Append(">() ?? new ").Append(module.OptionsTypeFullyQualifiedName).AppendLine("();");
                sb.Append("            ").Append(varName).Append(" = new ").Append(module.FullyQualifiedName).Append("(").Append(varName).AppendLine("Options);");
            }
            else
            {
                sb.Append("            ").Append(varName).Append(" = new ").Append(module.FullyQualifiedName).AppendLine("();");
            }

            sb.Append("            ").Append(varName).AppendLine(".OnServiceConfiguration(builder);");
            sb.AppendLine("        }");

            if (i < sortedModules.Length - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine("        var app = builder.Build();");
        sb.AppendLine();

        // Application configuration phase
        foreach (var module in sortedModules)
        {
            var varName = ToCamelCase(module.TypeName);
            sb.Append("        ").Append(varName).AppendLine("?.OnApplicationConfiguration(app);");
        }

        sb.AppendLine();
        sb.AppendLine("        return app;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool IsModuleEnabled(");
        sb.AppendLine("        global::Microsoft.Extensions.Configuration.IConfiguration configuration,");
        sb.AppendLine("        string moduleName)");
        sb.AppendLine("    {");
        sb.AppendLine("        return configuration.GetValue<bool?>($\"Modules:{moduleName}:Enabled\") ?? true;");
        sb.AppendLine("    }");
        sb.Append("}");

        context.AddSource("WebApiModuleExtensions.g.cs", sb.ToString());
    }

    private static string GetModuleName(string typeName)
    {
        if (typeName.Length > 6 && typeName.EndsWith("Module"))
        {
            return typeName.Substring(0, typeName.Length - 6);
        }

        return typeName;
    }

    private static string ToCamelCase(string name)
    {
        if (name.Length == 0)
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string ForDisplay(string fullyQualifiedName)
    {
        if (fullyQualifiedName.StartsWith("global::"))
        {
            return fullyQualifiedName.Substring(8);
        }

        return fullyQualifiedName;
    }

    private const string IWebApiModuleSource =
        """
        // <auto-generated />
        #nullable enable

        namespace GroundControl.Host.Api;

        internal interface IWebApiModule
        {
            void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder);
            void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app);
        }
        """;

    private const string IWebApiModuleOfTOptionsSource =
        """
        // <auto-generated />
        #nullable enable

        namespace GroundControl.Host.Api;

        internal interface IWebApiModule<TOptions> : IWebApiModule where TOptions : class, new()
        {
        }
        """;

    private const string RunsAfterAttributeSource =
        """
        // <auto-generated />
        #nullable enable

        namespace GroundControl.Host.Api;

        [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class RunsAfterAttribute<T> : System.Attribute where T : IWebApiModule
        {
            public bool Required { get; init; }
        }
        """;

    private const string RunsBeforeAttributeSource =
        """
        // <auto-generated />
        #nullable enable

        namespace GroundControl.Host.Api;

        [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
        internal sealed class RunsBeforeAttribute<T> : System.Attribute where T : IWebApiModule
        {
        }
        """;

    private const string ConfigurationKeyAttributeSource =
        """
        // <auto-generated />
        #nullable enable

        namespace GroundControl.Host.Api;

        [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
        internal sealed class ConfigurationKeyAttribute : System.Attribute
        {
            public ConfigurationKeyAttribute(string key) => Key = key;
            public string Key { get; }
        }
        """;
}