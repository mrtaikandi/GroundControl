using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Activity;

internal interface ILiveActivityTracker : IAsyncDisposable
{
    LiveActivitySnapshot Current { get; }

    void ClientConnected();

    void ClientDisconnected();

    void RecordEvent();

    void RecordAuditRecord(AuditRecord record);

    IAsyncEnumerable<LiveActivityEvent> SubscribeAsync(CancellationToken cancellationToken = default);
}