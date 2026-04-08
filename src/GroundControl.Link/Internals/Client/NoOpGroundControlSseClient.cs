namespace GroundControl.Link.Internals.Client;

internal sealed class NoOpGroundControlSseClient : IGroundControlSseClient
{
    public static NoOpGroundControlSseClient Instance { get; } = new();

    /// <inheritdoc />
    public string? LastEventId { get; set; }

    public async IAsyncEnumerable<SseEvent> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}