namespace GroundControl.Link.Internals;

internal sealed class NoOpSseClient : ISseClient
{
    public static NoOpSseClient Instance { get; } = new();

    /// <inheritdoc />
    public string? LastEventId { get; set; }

    public async IAsyncEnumerable<SseEvent> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}