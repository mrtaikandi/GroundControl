namespace GroundControl.Host.Api.Generators.Internals.Generators;

/// <summary>
/// Represents a source emitter that generates a single source file.
/// Each implementation is a self-contained unit responsible for producing
/// one generated file identified by its <see cref="HintName"/>.
/// </summary>
internal interface ISourceEmitter
{
    /// <summary>
    /// Gets the hint name used to identify the generated source file (e.g., "MyType.g.cs").
    /// </summary>
    string HintName { get; }

    /// <summary>
    /// Generates and returns the source text for the file.
    /// </summary>
    string Emit();
}