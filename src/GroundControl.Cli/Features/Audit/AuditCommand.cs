using System.CommandLine;
using GroundControl.Cli.Features.Audit.Get;
using GroundControl.Cli.Features.Audit.List;

namespace GroundControl.Cli.Features.Audit;

[RootCommand]
internal sealed class AuditCommand : Command
{
    public AuditCommand()
        : base("audit", "View audit records")
    {
        Subcommands.Add(new ListAuditRecordsCommand());
        Subcommands.Add(new GetAuditRecordCommand());
    }
}