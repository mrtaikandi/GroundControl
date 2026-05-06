using Testcontainers.Redis;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle.Redis;

/// <summary>
/// Shared assembly-level fixture that boots a Redis container for Data Protection
/// lifecycle tests. The same container is reused across tests to amortise startup cost;
/// each test isolates itself by using a unique <c>KeyName</c> on the same Redis instance.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}