namespace GroundControl.Host.Api.Generators.Internals;

internal static class KnownTypes
{
    internal const string ApiHostNamespace = "GroundControl.Host.Api";
    internal const string ModuleInterfaceMetadataName = "GroundControl.Host.Api.IWebApiModule";
    internal const string GenericModuleInterfaceMetadataName = "GroundControl.Host.Api.IWebApiModule`1";
    internal const string RunsAfterAttributeMetadataName = "GroundControl.Host.Api.RunsAfterAttribute`1";
    internal const string RunsBeforeAttributeMetadataName = "GroundControl.Host.Api.RunsBeforeAttribute`1";
    internal const string ConfigurationKeyAttributeMetadataName = "GroundControl.Host.Api.ConfigurationKeyAttribute";

    internal const string WebApplication = "Microsoft.AspNetCore.Builder.WebApplication";
    internal const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
    internal const string IConfiguration = "Microsoft.Extensions.Configuration.IConfiguration";

    internal const string NotNullIfNotNullAttributeFullName = "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute";
    internal const string CompilerGeneratedAttributeFullName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
    internal const string ArgumentNullException = "System.ArgumentNullException";
    internal const string ArgumentOutOfRangeException = "System.ArgumentOutOfRangeException";
    internal const string InvalidOperationException = "System.InvalidOperationException";
    internal const string ModuleConfigurationException = $"{ApiHostNamespace}.ModuleConfigurationException";
}