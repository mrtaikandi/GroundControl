using Microsoft.CodeAnalysis;

namespace GroundControl.Host.Api.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor CircularDependency = new(
        id: "GCA001",
        title: "Circular dependency detected",
        messageFormat: "Module '{0}' has a circular dependency involving '{1}'",
        category: "GroundControl.ModuleBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidConstructor = new(
        id: "GCA002",
        title: "Invalid module constructor",
        messageFormat: "{0}",
        category: "GroundControl.ModuleBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateDependency = new(
        id: "GCA003",
        title: "Duplicate dependency",
        messageFormat: "Module '{0}' has duplicate RunsAfter/RunsBefore targeting '{1}'",
        category: "GroundControl.ModuleBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DependencyNotFound = new(
        id: "GCA004",
        title: "Dependency target not found",
        messageFormat: "Module '{0}' references '{1}' in RunsAfter/RunsBefore but it was not found in discovered modules",
        category: "GroundControl.ModuleBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}