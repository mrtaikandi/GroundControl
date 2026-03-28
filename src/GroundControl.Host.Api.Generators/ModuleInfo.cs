using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GroundControl.Host.Api.Generators;

internal readonly struct DependencyInfo : IEquatable<DependencyInfo>
{
    public string TargetFullyQualifiedName { get; }

    public bool Required { get; }

    public DependencyInfo(string targetFullyQualifiedName, bool required)
    {
        TargetFullyQualifiedName = targetFullyQualifiedName;
        Required = required;
    }

    public bool Equals(DependencyInfo other) =>
        TargetFullyQualifiedName == other.TargetFullyQualifiedName &&
        Required == other.Required;

    public override bool Equals(object obj) => obj is DependencyInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((TargetFullyQualifiedName != null ? TargetFullyQualifiedName.GetHashCode() : 0) * 397) ^ Required.GetHashCode();
        }
    }

    public static bool operator ==(DependencyInfo left, DependencyInfo right) => left.Equals(right);

    public static bool operator !=(DependencyInfo left, DependencyInfo right) => !left.Equals(right);
}

internal readonly struct ModuleInfo : IEquatable<ModuleInfo>
{
    public string FullyQualifiedName { get; }

    public string TypeName { get; }

    public ImmutableArray<DependencyInfo> RunsAfter { get; }

    public ImmutableArray<string> RunsBefore { get; }

    public string? LocationFilePath { get; }

    public TextSpan LocationSpan { get; }

    public LinePositionSpan LocationLineSpan { get; }

    public string? OptionsTypeFullyQualifiedName { get; }

    public string? OptionsTypeName { get; }

    public string? ConfigurationSectionName { get; }

    public bool HasOptions => OptionsTypeFullyQualifiedName is not null;

    public ModuleInfo(
        string fullyQualifiedName,
        string typeName,
        ImmutableArray<DependencyInfo> runsAfter,
        ImmutableArray<string> runsBefore,
        string? locationFilePath,
        TextSpan locationSpan,
        LinePositionSpan locationLineSpan,
        string? optionsTypeFullyQualifiedName,
        string? optionsTypeName,
        string? configurationSectionName)
    {
        FullyQualifiedName = fullyQualifiedName;
        TypeName = typeName;
        RunsAfter = runsAfter;
        RunsBefore = runsBefore;
        LocationFilePath = locationFilePath;
        LocationSpan = locationSpan;
        LocationLineSpan = locationLineSpan;
        OptionsTypeFullyQualifiedName = optionsTypeFullyQualifiedName;
        OptionsTypeName = optionsTypeName;
        ConfigurationSectionName = configurationSectionName;
    }

    public Location GetLocation() =>
        LocationFilePath is not null
            ? Location.Create(LocationFilePath, LocationSpan, LocationLineSpan)
            : Location.None;

    public bool Equals(ModuleInfo other) =>
        FullyQualifiedName == other.FullyQualifiedName &&
        TypeName == other.TypeName &&
        RunsAfter.SequenceEqual(other.RunsAfter) &&
        RunsBefore.SequenceEqual(other.RunsBefore) &&
        LocationFilePath == other.LocationFilePath &&
        LocationSpan.Equals(other.LocationSpan) &&
        LocationLineSpan.Equals(other.LocationLineSpan) &&
        OptionsTypeFullyQualifiedName == other.OptionsTypeFullyQualifiedName &&
        OptionsTypeName == other.OptionsTypeName &&
        ConfigurationSectionName == other.ConfigurationSectionName;

    public override bool Equals(object obj) => obj is ModuleInfo other && Equals(other);

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
            return hash;
        }
    }

    public static bool operator ==(ModuleInfo left, ModuleInfo right) => left.Equals(right);

    public static bool operator !=(ModuleInfo left, ModuleInfo right) => !left.Equals(right);
}

internal readonly struct ModuleResult : IEquatable<ModuleResult>
{
    public ModuleInfo Module { get; }

    public string? ConstructorErrorMessage { get; }

    public bool IsValid => ConstructorErrorMessage is null;

    public ModuleResult(ModuleInfo module, string? constructorErrorMessage)
    {
        Module = module;
        ConstructorErrorMessage = constructorErrorMessage;
    }

    public bool Equals(ModuleResult other) =>
        Module.Equals(other.Module) &&
        ConstructorErrorMessage == other.ConstructorErrorMessage;

    public override bool Equals(object obj) => obj is ModuleResult other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Module.GetHashCode() * 397) ^ (ConstructorErrorMessage != null ? ConstructorErrorMessage.GetHashCode() : 0);
        }
    }

    public static bool operator ==(ModuleResult left, ModuleResult right) => left.Equals(right);

    public static bool operator !=(ModuleResult left, ModuleResult right) => !left.Equals(right);
}