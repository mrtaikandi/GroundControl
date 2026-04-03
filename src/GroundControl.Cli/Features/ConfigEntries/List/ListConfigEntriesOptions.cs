using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.ConfigEntries.List;

internal sealed class ListConfigEntriesOptions
{
    public Guid? OwnerId { get; set; }

    public ConfigEntryOwnerType? OwnerType { get; set; }

    public string? KeyPrefix { get; set; }

    public bool? Decrypt { get; set; }
}