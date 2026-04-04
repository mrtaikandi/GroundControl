#:property PublishAot=false
#:property NoWarn=CA1849;CA2007;CA1031;CA1303;IDE0011;CA1305
#:package Spectre.Console

using System.Diagnostics;
using System.Text.RegularExpressions;
using Spectre.Console;

// ─── Argument Parsing ────────────────────────────────────────────────────────

var cliPath = "groundcontrol";
string? serverUrl = null;
var seedPassword = "Test!Password123";
var count = 1;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--cli" when i + 1 < args.Length:
            cliPath = args[++i];
            break;
        case "--server-url" when i + 1 < args.Length:
            serverUrl = args[++i];
            break;
        case "--seed-password" when i + 1 < args.Length:
            seedPassword = args[++i];
            break;
        case "--count" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out count) || count < 1)
            {
                AnsiConsole.MarkupLine("[red]--count must be a positive integer.[/]");
                return 1;
            }
            break;
        case "--help" or "-h":
            PrintHelp();
            return 0;
        default:
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown argument:[/] {args[i]}");
            AnsiConsole.MarkupLine("Run with [bold]--help[/] for usage.");
            return 1;
    }
}

// ─── Banner ──────────────────────────────────────────────────────────────────

AnsiConsole.Write(
    new Panel("[bold cyan]GroundControl[/] — Seed Data")
        .BorderColor(Color.Cyan1)
        .Padding(2, 0));

AnsiConsole.MarkupLineInterpolated($"  [dim]CLI:[/]    {cliPath}");
AnsiConsole.MarkupLineInterpolated($"  [dim]Count:[/]  {count} set(s) per entity type");
if (serverUrl is not null)
{
    AnsiConsole.MarkupLineInterpolated($"  [dim]Server:[/] {serverUrl}  [dim](user must already be logged in)[/]");
}

AnsiConsole.WriteLine();

// ─── Summary Tracking ────────────────────────────────────────────────────────

List<(string Label, Guid? Id, bool Created)> summary = [];

// ─── Phase 1: Scopes ─────────────────────────────────────────────────────────

PrintPhase("Scopes");
await RunAsync("Scope: Environment",
    ["scope", "create",
     "--dimension", "Environment",
     "--values", "dev,staging,prod",
     "--description", "Deployment environment tier"]);

await RunAsync("Scope: Region",
    ["scope", "create",
     "--dimension", "Region",
     "--values", "us-east,us-west,eu-west",
     "--description", "Geographic deployment region"]);

await RunAsync("Scope: AppTier",
    ["scope", "create",
     "--dimension", "AppTier",
     "--values", "frontend,backend,worker",
     "--description", "Application tier classification"]);

for (var iter = 1; iter <= count; iter++)
{
    await SeedSetAsync(iter);
}

// ─── Final Summary ────────────────────────────────────────────────────────────

AnsiConsole.WriteLine();

var summaryTable = new Table()
    .Title("[bold cyan]Summary[/]")
    .BorderColor(Color.Grey)
    .RoundedBorder()
    .AddColumn(new TableColumn("[bold]Status[/]").Centered().NoWrap())
    .AddColumn(new TableColumn("[bold]Entity[/]"))
    .AddColumn(new TableColumn("[bold]ID[/]").NoWrap());

var createdCount = 0;
var skippedCount = 0;

foreach (var (label, id, created) in summary)
{
    if (created)
    {
        summaryTable.AddRow(
            "[green]✓[/]",
            Markup.Escape(label),
            $"[dim]{id?.ToString() ?? string.Empty}[/]");
        createdCount++;
    }
    else
    {
        summaryTable.AddRow(
            "[yellow]~[/]",
            $"[dim]{Markup.Escape(label)}[/]",
            "[dim]skipped[/]");
        skippedCount++;
    }
}

AnsiConsole.Write(summaryTable);
AnsiConsole.MarkupLine($"\n  [bold green]Created:[/] {createdCount}  [bold]|[/]  [dim]Skipped/Failed:[/] {skippedCount}");
AnsiConsole.WriteLine();

return 0;
async Task SeedSetAsync(int iter)
{
    var s = count > 1 ? $" {iter}" : "";
    var setLabel = count > 1 ? $" (set {iter}/{count})" : "";

    PrintPhase($"Groups{setLabel}");
    var platformTeamId = await CreateAsync($"Group: Platform Team{s}",
        ["group", "create",
         "--name", $"Platform Team{s}",
         "--description", "Platform engineering team"]);

    var devTeamId = await CreateAsync($"Group: Development Team{s}",
        ["group", "create",
         "--name", $"Development Team{s}",
         "--description", "Application development team"]);

    PrintPhase($"Variables{setLabel}");
    if (platformTeamId is not null)
    {
        var gid = platformTeamId.Value.ToString();

        await RunAsync($"Variable: DatabaseConnectionString{s}",
            ["variable", "create",
             "--name", $"DatabaseConnectionString{s}",
             "--scope", "Global",
             "--sensitive",
             "--description", "Primary database connection string",
             "--value", "default=Server=localhost;Database=groundcontrol;User Id=app;",
             "--value", "Environment:prod=Server=prod-db.internal;Database=groundcontrol;User Id=app;Encrypt=True;",
             "--group-id", gid]);

        await RunAsync($"Variable: ApiRateLimit{s}",
            ["variable", "create",
             "--name", $"ApiRateLimit{s}",
             "--scope", "Global",
             "--description", "Maximum API requests allowed per minute",
             "--value", "default=100",
             "--value", "Environment:prod=1000",
             "--group-id", gid]);

        await RunAsync($"Variable: LogLevel{s}",
            ["variable", "create",
             "--name", $"LogLevel{s}",
             "--scope", "Global",
             "--description", "Application structured log level",
             "--value", "Environment:dev=Debug",
             "--value", "Environment:staging=Information",
             "--value", "Environment:prod=Warning",
             "--group-id", gid]);
    }
    else
    {
        PrintSkip($"Variables (all){s}", "Platform Team group was not created");
    }

    PrintPhase($"Templates{setLabel}");
    Guid? baseTemplateId = null;
    Guid? dbTemplateId = null;

    if (platformTeamId is not null)
    {
        var gid = platformTeamId.Value.ToString();

        baseTemplateId = await CreateAsync($"Template: Base Application Template{s}",
            ["template", "create",
             "--name", $"Base Application Template{s}",
             "--description", "Core configuration settings shared by all applications",
             "--group-id", gid]);

        dbTemplateId = await CreateAsync($"Template: Database Template{s}",
            ["template", "create",
             "--name", $"Database Template{s}",
             "--description", "Database connectivity and schema settings",
             "--group-id", gid]);
    }
    else
    {
        PrintSkip($"Templates (all){s}", "Platform Team group was not created");
    }

    PrintPhase($"Config Entries{setLabel}");
    if (baseTemplateId is not null)
    {
        var oid = baseTemplateId.Value.ToString();

        await RunAsync($"Config Entry: app.name{s}",
            ["config-entry", "create",
             "--key", "app.name",
             "--value-type", "String",
             "--description", "Application display name",
             "--owner-id", oid,
             "--owner-type", "Template"]);

        await RunAsync($"Config Entry: app.version{s}",
            ["config-entry", "create",
             "--key", "app.version",
             "--value-type", "String",
             "--description", "Deployed application version",
             "--owner-id", oid,
             "--owner-type", "Template"]);

        await RunAsync($"Config Entry: app.debug{s}",
            ["config-entry", "create",
             "--key", "app.debug",
             "--value-type", "Boolean",
             "--description", "Enable verbose debug output",
             "--value", "default=false",
             "--value", "Environment:dev=true",
             "--owner-id", oid,
             "--owner-type", "Template"]);
    }
    else
    {
        PrintSkip($"Config Entries — Base Application Template{s}", "Template was not created");
    }

    if (dbTemplateId is not null)
    {
        var oid = dbTemplateId.Value.ToString();

        await RunAsync($"Config Entry: db.host{s}",
            ["config-entry", "create",
             "--key", "db.host",
             "--value-type", "String",
             "--description", "Database server hostname",
             "--value", "default=localhost",
             "--value", "Environment:staging=staging-db.internal",
             "--value", "Environment:prod=prod-db.internal",
             "--owner-id", oid,
             "--owner-type", "Template"]);

        await RunAsync($"Config Entry: db.port{s}",
            ["config-entry", "create",
             "--key", "db.port",
             "--value-type", "Int32",
             "--description", "Database server port",
             "--value", "default=5432",
             "--owner-id", oid,
             "--owner-type", "Template"]);

        await RunAsync($"Config Entry: db.name{s}",
            ["config-entry", "create",
             "--key", "db.name",
             "--value-type", "String",
             "--description", "Database name",
             "--value", "default=app",
             "--owner-id", oid,
             "--owner-type", "Template"]);

        await RunAsync($"Config Entry: db.schema{s}",
            ["config-entry", "create",
             "--key", "db.schema",
             "--value-type", "String",
             "--description", "Database schema name",
             "--value", "default=public",
             "--owner-id", oid,
             "--owner-type", "Template"]);
    }
    else
    {
        PrintSkip($"Config Entries — Database Template{s}", "Template was not created");
    }

    PrintPhase($"Users{setLabel}");
    if (devTeamId is not null)
    {
        var gid = devTeamId.Value.ToString();
        var u = count > 1 ? iter.ToString() : "";

        await RunAsync($"User: developer{s}",
            ["user", "create",
             "--username", $"developer{u}",
             "--email", $"developer{u}@example.local",
             "--first-name", "Dev",
             "--last-name", "User",
             "--password", seedPassword,
             "--group-id", gid]);

        await RunAsync($"User: testadmin{s}",
            ["user", "create",
             "--username", $"testadmin{u}",
             "--email", $"testadmin{u}@example.local",
             "--first-name", "Test",
             "--last-name", "Admin",
             "--password", seedPassword,
             "--group-id", gid]);
    }
    else
    {
        PrintSkip($"Users (all){s}", "Development Team group was not created");
    }

    PrintPhase($"Projects{setLabel}");
    if (baseTemplateId is not null || dbTemplateId is not null)
    {
        var baseId = baseTemplateId?.ToString() ?? "";
        var dbId = dbTemplateId?.ToString() ?? "";

        if (baseTemplateId is not null)
        {
            await RunAsync($"Project: Frontend App{s}",
                ["project", "create",
                 "--name", $"Frontend App{s}",
                 "--description", "Customer-facing web application",
                 "--template-ids", baseId]);
        }

        if (baseTemplateId is not null && dbTemplateId is not null)
        {
            await RunAsync($"Project: Backend API{s}",
                ["project", "create",
                 "--name", $"Backend API{s}",
                 "--description", "Core REST API service",
                 "--template-ids", $"{baseId},{dbId}"]);
        }

        if (dbTemplateId is not null)
        {
            await RunAsync($"Project: Worker Service{s}",
                ["project", "create",
                 "--name", $"Worker Service{s}",
                 "--description", "Background job processing service",
                 "--template-ids", dbId]);
        }
    }
    else
    {
        PrintSkip($"Projects (all){s}", "No templates were created");
    }
}
// ─── Helpers ──────────────────────────────────────────────────────────────────

static void PrintHelp()
{
    AnsiConsole.Write(
        new Panel(
            new Rows(
                new Markup("[bold]Usage:[/]"),
                new Markup("  dotnet .tools/seed.cs [[-- [[options]]]]\n"),
                new Markup("[bold]Options:[/]"),
                new Markup("  [cyan]--cli[/] [dim]<path>[/]             Path to the [bold]groundcontrol[/] CLI executable"),
                new Markup("                           [dim](default: groundcontrol, resolved from PATH)[/]"),
                new Markup("  [cyan]--server-url[/] [dim]<url>[/]       Server URL (banner only). Log in first:"),
                new Markup("                           [dim]groundcontrol auth login --server-url <url>[/]"),
                new Markup("  [cyan]--seed-password[/] [dim]<pwd>[/]    Password for seeded user accounts"),
                new Markup("                           [dim](default: Test!Password123)[/]"),
                new Markup("  [cyan]--count[/] [dim]<n>[/]              Number of entity sets to seed"),
                new Markup("                           [dim](default: 1; scopes always created once)[/]"),
                new Markup("  [cyan]-h, --help[/]               Show this help\n"),
                new Markup("[bold]Entities seeded (in dependency order):[/]"),
                new Markup("  [yellow]Groups[/]         Platform Team, Development Team"),
                new Markup("  [yellow]Scopes[/]         Environment (dev/staging/prod), Region, AppTier"),
                new Markup("  [yellow]Variables[/]      DatabaseConnectionString, ApiRateLimit, LogLevel"),
                new Markup("  [yellow]Templates[/]      Base Application Template, Database Template"),
                new Markup("  [yellow]Config Entries[/] app.name/version/debug, db.host/port/name/schema"),
                new Markup("  [yellow]Users[/]          developer, testadmin"),
                new Markup("  [yellow]Projects[/]       Frontend App, Backend API, Worker Service\n"),
                new Markup("[dim]Failed creates are non-fatal — the script continues and marks the step\nas skipped. Re-running against a server with existing data is safe.[/]")))
            .Header("[bold cyan]GroundControl — Seed Data[/]")
            .BorderColor(Color.Cyan1)
            .Padding(1, 1));
}

void PrintPhase(string name)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[bold yellow]{Markup.Escape(name)}[/]") { Justification = Justify.Left });
}

void PrintSkip(string label, string reason)
{
    AnsiConsole.MarkupLineInterpolated($"  [yellow]~[/] [dim]{Markup.Escape(label)}[/]");
    AnsiConsole.MarkupLineInterpolated($"    [dim]Reason: {Markup.Escape(reason)}[/]");
    summary.Add((label, null, false));
}

async Task RunAsync(string label, string[] cliArgs)
{
    AnsiConsole.Markup($"  {Markup.Escape($"{label,-50}")}");

    var (success, stdout, stderr) = await RunCliAsync(cliArgs);

    if (success)
    {
        var id = ExtractId(stdout);
        AnsiConsole.MarkupLine(id.HasValue ? $"[green]✓[/]  [dim]{id}[/]" : "[green]✓[/]");
        summary.Add((label, id, true));
    }
    else
    {
        AnsiConsole.MarkupLine("[yellow]~  skipped[/]");
        var reason = FirstLine(stderr.Length > 0 ? stderr : stdout);
        AnsiConsole.MarkupLineInterpolated($"    [dim]{Markup.Escape(reason)}[/]");
        summary.Add((label, null, false));
    }
}

async Task<Guid?> CreateAsync(string label, string[] cliArgs)
{
    AnsiConsole.Markup($"  {Markup.Escape($"{label,-50}")}");

    var (success, stdout, stderr) = await RunCliAsync(cliArgs);

    if (success)
    {
        var id = ExtractId(stdout);
        AnsiConsole.MarkupLine(id.HasValue ? $"[green]✓[/]  [dim]{id}[/]" : "[green]✓[/]");
        summary.Add((label, id, true));
        return id;
    }
    else
    {
        AnsiConsole.MarkupLine("[yellow]~  skipped[/]");
        var reason = FirstLine(stderr.Length > 0 ? stderr : stdout);
        AnsiConsole.MarkupLineInterpolated($"    [dim]{Markup.Escape(reason)}[/]");
        summary.Add((label, null, false));
        return null;
    }
}

async Task<(bool Success, string Stdout, string Stderr)> RunCliAsync(string[] cliArgs)
{
    var psi = new ProcessStartInfo(cliPath)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    // Always suppress interactive prompts so the process never hangs
    psi.ArgumentList.Add("--no-interactive");
    foreach (var arg in cliArgs)
    {
        psi.ArgumentList.Add(arg);
    }

    Process process;
    try
    {
        process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{cliPath}'.");
    }
    catch (Exception ex)
    {
        return (false, string.Empty, $"Could not launch CLI: {ex.Message}");
    }

    using (process)
    {
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode == 0, await stdoutTask, await stderrTask);
    }
}

static Guid? ExtractId(string stdout)
{
    var match = Regex.Match(
        stdout,
        @"\(id:\s*([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
        RegexOptions.IgnoreCase);

    return match.Success && Guid.TryParse(match.Groups[1].Value, out var id) ? id : null;
}

static string FirstLine(string text)
{
    const int MaxLen = 110;
    var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? text.Trim();
    return line.Length > MaxLen ? line[..MaxLen] + "…" : line;
}