using System.Collections.Immutable;
using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;
using GroundControl.Host.Api.Generators.WebApiModule.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GroundControl.Host.Api.Generators.WebApiModule;

[Generator]
public class WebApiModuleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.GenerateSource(new WebApiModuleInterfaceEmitter());
            ctx.GenerateSource(new WebApiModuleGenericInterfaceEmitter());
            ctx.GenerateSource(new RunsAfterAttributeEmitter());
            ctx.GenerateSource(new RunsBeforeAttributeEmitter());
            ctx.GenerateSource(new ConfigurationKeyAttributeEmitter());
            ctx.GenerateSource(new ModuleConfigurationExceptionEmitter());
        });

        var moduleResults = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => WebApiModuleParser.TransformToModuleResult(ctx, ct))
            .Where(static r => r.HasValue)
            .Select(static (r, _) => r.GetValueOrDefault());

        var assemblyName = context.CompilationProvider.Select(
            static (c, _) => c.AssemblyName ?? "GeneratedNamespace");

        var combined = moduleResults.Collect().Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<ModuleResult> results, string rootNamespace)
    {
        if (results.IsDefaultOrEmpty)
        {
            return;
        }

        var validModules = new List<ModuleDescriptor>();
        var hasErrors = false;

        // Report GCA002 for constructor errors
        foreach (var result in results)
        {
            if (!result.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidConstructor,
                    result.Module.GetLocation(),
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
                        module.GetLocation(),
                        module.TypeName,
                        ForDisplay(dep.TargetFullyQualifiedName)));
                    hasErrors = true;
                }

                if (!allModuleNames.Contains(dep.TargetFullyQualifiedName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DependencyNotFound,
                        module.GetLocation(),
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
                        module.GetLocation(),
                        module.TypeName,
                        ForDisplay(target)));
                    hasErrors = true;
                }

                if (!allModuleNames.Contains(target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DependencyNotFound,
                        module.GetLocation(),
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
                        module.GetLocation(),
                        module.TypeName,
                        participantList));
                }
            }

            return;
        }

        context.GenerateSource(new WebApiModuleExtensionsEmitter(sortResult.SortedModules, rootNamespace));
    }

    private static string ForDisplay(string fullyQualifiedName) =>
        fullyQualifiedName.StartsWith("global::") ? fullyQualifiedName.Substring(8) : fullyQualifiedName;
}