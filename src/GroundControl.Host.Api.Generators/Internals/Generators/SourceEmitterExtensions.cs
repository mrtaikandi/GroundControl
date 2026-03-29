using Microsoft.CodeAnalysis;

namespace GroundControl.Host.Api.Generators.Internals.Generators;

/// <summary>
/// Extension methods for emitting generated source via <see cref="ISourceEmitter"/> implementations.
/// </summary>
internal static class SourceEmitterExtensions
{
    /// <summary>
    /// Emits the source produced by the <paramref name="emitter"/> into the post-initialization output.
    /// </summary>
    /// <param name="context">The post-initialization context to add the source to.</param>
    /// <param name="emitter">The source emitter that produces the generated file.</param>
    internal static void GenerateSource(
        this IncrementalGeneratorPostInitializationContext context,
        ISourceEmitter emitter)
    {
        context.AddSource(emitter.HintName, emitter.Emit());
    }

    /// <summary>
    /// Emits the source produced by the <paramref name="emitter"/> into the source production output.
    /// </summary>
    /// <param name="context">The source production context to add the source to.</param>
    /// <param name="emitter">The source emitter that produces the generated file.</param>
    internal static void GenerateSource(
        this SourceProductionContext context,
        ISourceEmitter emitter)
    {
        context.AddSource(emitter.HintName, emitter.Emit());
    }
}