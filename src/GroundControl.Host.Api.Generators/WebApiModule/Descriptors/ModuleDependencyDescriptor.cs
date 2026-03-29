namespace GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

internal readonly record struct ModuleDependencyDescriptor(string TargetFullyQualifiedName, bool Required);