#pragma warning disable CA1813

namespace GroundControl.Host.Cli;

/// <summary>
/// Marks a class as a root command for the CLI application.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class RootCommandAttribute : Attribute;

/// <summary>
/// Marks a class as a root command for the CLI application.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class RootCommandAttribute<TDependencyModule> : RootCommandAttribute
    where TDependencyModule : IDependencyModule;