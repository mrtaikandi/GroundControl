using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

internal readonly record struct ModuleDescriptor(
    string FullyQualifiedName,
    string TypeName,
    ImmutableArray<ModuleDependencyDescriptor> RunsAfter,
    ImmutableArray<string> RunsBefore,
    string? LocationFilePath,
    TextSpan LocationSpan,
    LinePositionSpan LocationLineSpan,
    string? OptionsTypeFullyQualifiedName,
    string? OptionsTypeName,
    string? ConfigurationSectionName,
    ImmutableArray<PropertyConfigurationOverrideDescriptor> PropertyConfigurationOverrides)
{
    public bool HasOptions => OptionsTypeFullyQualifiedName is not null;

    public Location GetLocation() =>
        LocationFilePath is not null
            ? Location.Create(LocationFilePath, LocationSpan, LocationLineSpan)
            : Location.None;

    public bool Equals(ModuleDescriptor other) =>
        FullyQualifiedName == other.FullyQualifiedName &&
        TypeName == other.TypeName &&
        RunsAfter.SequenceEqual(other.RunsAfter) &&
        RunsBefore.SequenceEqual(other.RunsBefore) &&
        LocationFilePath == other.LocationFilePath &&
        LocationSpan.Equals(other.LocationSpan) &&
        LocationLineSpan.Equals(other.LocationLineSpan) &&
        OptionsTypeFullyQualifiedName == other.OptionsTypeFullyQualifiedName &&
        OptionsTypeName == other.OptionsTypeName &&
        ConfigurationSectionName == other.ConfigurationSectionName &&
        PropertyConfigurationOverrides.SequenceEqual(other.PropertyConfigurationOverrides);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FullyQualifiedName != null ? FullyQualifiedName.GetHashCode() : 0;
            hash = (hash * 397) ^ (TypeName != null ? TypeName.GetHashCode() : 0);
            hash = (hash * 397) ^ RunsAfter.Length;
            hash = (hash * 397) ^ RunsBefore.Length;
            hash = (hash * 397) ^ (LocationFilePath != null ? LocationFilePath.GetHashCode() : 0);
            hash = (hash * 397) ^ LocationSpan.GetHashCode();
            hash = (hash * 397) ^ LocationLineSpan.GetHashCode();
            hash = (hash * 397) ^ (OptionsTypeFullyQualifiedName != null ? OptionsTypeFullyQualifiedName.GetHashCode() : 0);
            hash = (hash * 397) ^ (OptionsTypeName != null ? OptionsTypeName.GetHashCode() : 0);
            hash = (hash * 397) ^ (ConfigurationSectionName != null ? ConfigurationSectionName.GetHashCode() : 0);
            hash = (hash * 397) ^ PropertyConfigurationOverrides.Length;
            return hash;
        }
    }
}