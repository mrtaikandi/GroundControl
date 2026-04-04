namespace GroundControl.Cli.Features.Tui.ViewModels;

internal abstract class ReadOnlyResourceViewModel<T> : ResourceViewModel<T>
{
    internal sealed override IReadOnlyList<FieldDefinition> GetFormFields() =>
        throw new NotSupportedException($"{ResourceTypeName} is read-only.");

    internal sealed override IReadOnlyList<FieldDefinition> GetEditFormFields(T item) =>
        throw new NotSupportedException($"{ResourceTypeName} is read-only.");

    internal sealed override Task CreateAsync(Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{ResourceTypeName} is read-only.");

    internal sealed override Task UpdateAsync(T item, Dictionary<string, string> fieldValues, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{ResourceTypeName} is read-only.");

    internal sealed override Task DeleteAsync(T item, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{ResourceTypeName} is read-only.");
}