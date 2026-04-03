using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.ConfigEntries.Create;

internal sealed class CreateConfigEntryOptions
{
    public string? Key { get; set; }

    public Guid? OwnerId { get; set; }

    public ConfigEntryOwnerType? OwnerType { get; set; }

    public string? ValueType { get; set; }

    public bool? Sensitive { get; set; }

    public string? Description { get; set; }

    public string[]? Values { get; set; }

    public string? ValuesJson { get; set; }
}