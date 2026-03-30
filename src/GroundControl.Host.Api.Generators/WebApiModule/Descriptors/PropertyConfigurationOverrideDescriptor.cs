namespace GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

internal readonly record struct PropertyConfigurationOverrideDescriptor(
    string PropertyName,
    string PropertyTypeFullyQualifiedName,
    string ConfigurationSectionKey);