namespace GroundControl.Host.Api.Generators;

internal readonly record struct ModuleResult(ModuleInfo Module, string? ConstructorErrorMessage)
{
    public bool IsValid => ConstructorErrorMessage is null;
}