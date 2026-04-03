using System.CommandLine;
using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.ConfigEntries.List;

internal sealed class ListConfigEntriesCommand : Command<ListConfigEntriesHandler, ListConfigEntriesOptions>
{
    public ListConfigEntriesCommand()
        : base("list", "List configuration entries")
    {
        var ownerIdOption = new Option<Guid?>("--owner-id", "Filter by owner ID");
        var ownerTypeOption = new Option<ConfigEntryOwnerType?>("--owner-type", "Filter by owner type (Template or Project)");
        var keyPrefixOption = new Option<string?>("--key-prefix", "Filter by key prefix");
        var decryptOption = new Option<bool?>("--decrypt", "Decrypt sensitive values");

        Options.Add(ownerIdOption);
        Options.Add(ownerTypeOption);
        Options.Add(keyPrefixOption);
        Options.Add(decryptOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.OwnerId = parseResult.GetValue(ownerIdOption);
            options.OwnerType = parseResult.GetValue(ownerTypeOption);
            options.KeyPrefix = parseResult.GetValue(keyPrefixOption);
            options.Decrypt = parseResult.GetValue(decryptOption);
        });
    }
}