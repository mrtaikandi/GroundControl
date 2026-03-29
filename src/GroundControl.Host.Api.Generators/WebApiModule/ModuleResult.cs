using GroundControl.Host.Api.Generators.WebApiModule.Descriptors;

namespace GroundControl.Host.Api.Generators.WebApiModule;

internal readonly record struct ModuleResult(ModuleDescriptor Module, string? ConstructorErrorMessage)
{
    public bool IsValid => ConstructorErrorMessage is null;
}