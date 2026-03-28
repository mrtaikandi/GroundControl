namespace GroundControl.Host.Api.Generators;

internal readonly record struct DependencyInfo(string TargetFullyQualifiedName, bool Required);