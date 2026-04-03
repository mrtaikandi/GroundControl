using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace GroundControl.Cli.Features.Audit.Get;

internal sealed class GetAuditRecordHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly GetAuditRecordOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public GetAuditRecordHandler(
        IShell shell,
        IOptions<GetAuditRecordOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var record = await _client.GetAuditRecordHandlerAsync(_options.Id, cancellationToken);

            if (_hostOptions.OutputFormat == OutputFormat.Json)
            {
                _shell.RenderJson(record);
                return 0;
            }

            _shell.RenderDetail(BuildDetail(record), _hostOptions.OutputFormat);

            if (record.Changes.Count > 0)
            {
                _shell.DisplayEmptyLine();
                RenderFieldChanges(record.Changes);
            }

            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }

    private static IReadOnlyList<(string Key, string Value)> BuildDetail(AuditRecordResponse record) =>
    [
        ("Id", record.Id.ToString()),
        ("Entity Type", record.EntityType),
        ("Entity Id", record.EntityId.ToString()),
        ("Action", record.Action),
        ("Performed By", record.PerformedBy.ToString()),
        ("Performed At", record.PerformedAt.ToString("O"))
    ];

    private void RenderFieldChanges(ICollection<FieldChangeResponse> changes)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("Field").NoWrap());
        table.AddColumn(new TableColumn("Old Value"));
        table.AddColumn(new TableColumn("New Value"));

        foreach (var change in changes)
        {
            table.AddRow(
                Markup.Escape(change.Field),
                $"[red]{Markup.Escape(change.OldValue ?? string.Empty)}[/]",
                $"[green]{Markup.Escape(change.NewValue ?? string.Empty)}[/]");
        }

        _shell.Console.Write(table);
    }
}