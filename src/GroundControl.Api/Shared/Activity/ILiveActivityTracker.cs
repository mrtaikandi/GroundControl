namespace GroundControl.Api.Shared.Activity;

internal interface ILiveActivityTracker : IAsyncDisposable
{
    LiveActivitySnapshot Current { get; }

    void ClientConnected();

    void ClientDisconnected();

    void RecordEvent();

    IAsyncEnumerable<LiveActivitySnapshot> SubscribeAsync(CancellationToken cancellationToken = default);
}