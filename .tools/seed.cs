#:property PublishAot=false
#:property NoWarn=CA1849;CA2007;CA1031;CA1303;IDE0011

using System.Diagnostics;
using System.Text.RegularExpressions;

// ─── Argument Parsing ────────────────────────────────────────────────────────

var cliPath = "groundcontrol";
string? serverUrl = null;
var seedPassword = "Test!Password123";

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
        case "--help" or "-h":
            PrintHelp();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 1;
    }
}

// ─── Banner ──────────────────────────────────────────────────────────────────

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║         GroundControl — Seed Data            ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine($"  CLI    : {cliPath}");

if (serverUrl is not null)
{
    Console.WriteLine($"  Server : {serverUrl}  (user must already be logged in)");
}

Console.WriteLine();

// ─── Summary Tracking ────────────────────────────────────────────────────────

List<(string Label, Guid? Id, bool Created)> summary = [];

// ─── Phase 1: Groups ─────────────────────────────────────────────────────────

PrintPhase("Groups");
var platformTeamId = await CreateAsync("Group: Platform Team",
    ["group", "create",
     "--name", "Platform Team",
     "--description", "Platform engineering team"]);

var devTeamId = await CreateAsync("Group: Development Team",
    ["group", "create",
     "--name", "Development Team",
     "--description", "Application development team"]);

// ─── Phase 2: Scopes ─────────────────────────────────────────────────────────

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

// ─── Phase 3: Variables ───────────────────────────────────────────────────────

PrintPhase("Variables");
if (platformTeamId is not null)
{
    var gid = platformTeamId.Value.ToString();

    await RunAsync("Variable: DatabaseConnectionString",
        ["variable", "create",
         "--name", "DatabaseConnectionString",
         "--scope", "Global",
         "--sensitive",
         "--description", "Primary database connection string",
         "--value", "default=Server=localhost;Database=groundcontrol;User Id=app;",
         "--value", "Environment:prod=Server=prod-db.internal;Database=groundcontrol;User Id=app;Encrypt=True;",
         "--group-id", gid]);

    await RunAsync("Variable: ApiRateLimit",
        ["variable", "create",
         "--name", "ApiRateLimit",
         "--scope", "Global",
         "--description", "Maximum API requests allowed per minute",
         "--value", "default=100",
         "--value", "Environment:prod=1000",
         "--group-id", gid]);

    await RunAsync("Variable: LogLevel",
        ["variable", "create",
         "--name", "LogLevel",
         "--scope", "Global",
         "--description", "Application structured log level",
         "--value", "Environment:dev=Debug",
         "--value", "Environment:staging=Information",
         "--value", "Environment:prod=Warning",
         "--group-id", gid]);
}
else
{
    PrintSkip("Variables (all)", "Platform Team group was not created");
}

// ─── Phase 4: Templates ───────────────────────────────────────────────────────

PrintPhase("Templates");
Guid? baseTemplateId = null;
Guid? dbTemplateId = null;

if (platformTeamId is not null)
{
    var gid = platformTeamId.Value.ToString();

    baseTemplateId = await CreateAsync("Template: Base Application Template",
        ["template", "create",
         "--name", "Base Application Template",
         "--description", "Core configuration settings shared by all applications",
         "--group-id", gid]);

    dbTemplateId = await CreateAsync("Template: Database Template",
        ["template", "create",
         "--name", "Database Template",
         "--description", "Database connectivity and schema settings",
         "--group-id", gid]);
}
else
{
    PrintSkip("Templates (all)", "Platform Team group was not created");
}

// ─── Phase 5: Config Entries ──────────────────────────────────────────────────

PrintPhase("Config Entries");
if (baseTemplateId is not null)
{
    var oid = baseTemplateId.Value.ToString();

    await RunAsync("Config Entry: app.name",
        ["config-entry", "create",
         "--key", "app.name",
         "--value-type", "String",
         "--description", "Application display name",
         "--owner-id", oid,
         "--owner-type", "Template"]);

    await RunAsync("Config Entry: app.version",
        ["config-entry", "create",
         "--key", "app.version",
         "--value-type", "String",
         "--description", "Deployed application version",
         "--owner-id", oid,
         "--owner-type", "Template"]);

    await RunAsync("Config Entry: app.debug",
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
    PrintSkip("Config Entries — Base Application Template", "Template was not created");
}

if (dbTemplateId is not null)
{
    var oid = dbTemplateId.Value.ToString();

    await RunAsync("Config Entry: db.host",
        ["config-entry", "create",
         "--key", "db.host",
         "--value-type", "String",
         "--description", "Database server hostname",
         "--value", "default=localhost",
         "--value", "Environment:staging=staging-db.internal",
         "--value", "Environment:prod=prod-db.internal",
         "--owner-id", oid,
         "--owner-type", "Template"]);

    await RunAsync("Config Entry: db.port",
        ["config-entry", "create",
         "--key", "db.port",
         "--value-type", "Int32",
         "--description", "Database server port",
         "--value", "default=5432",
         "--owner-id", oid,
         "--owner-type", "Template"]);

    await RunAsync("Config Entry: db.name",
        ["config-entry", "create",
         "--key", "db.name",
         "--value-type", "String",
         "--description", "Database name",
         "--value", "default=app",
         "--owner-id", oid,
         "--owner-type", "Template"]);

    await RunAsync("Config Entry: db.schema",
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
    PrintSkip("Config Entries — Database Template", "Template was not created");
}

// ─── Phase 6: Users ───────────────────────────────────────────────────────────

PrintPhase("Users");
if (devTeamId is not null)
{
    var gid = devTeamId.Value.ToString();

    await RunAsync("User: developer",
        ["user", "create",
         "--username", "developer",
         "--email", "developer@example.local",
         "--first-name", "Dev",
         "--last-name", "User",
         "--password", seedPassword,
         "--group-id", gid]);

    await RunAsync("User: testadmin",
        ["user", "create",
         "--username", "testadmin",
         "--email", "testadmin@example.local",
         "--first-name", "Test",
         "--last-name", "Admin",
         "--password", seedPassword,
         "--group-id", gid]);
}
else
{
    PrintSkip("Users (all)", "Development Team group was not created");
}

// ─── Phase 7: Projects ────────────────────────────────────────────────────────

PrintPhase("Projects");
if (baseTemplateId is not null || dbTemplateId is not null)
{
    var baseId = baseTemplateId?.ToString() ?? "";
    var dbId = dbTemplateId?.ToString() ?? "";

    if (baseTemplateId is not null)
    {
        await RunAsync("Project: Frontend App",
            ["project", "create",
             "--name", "Frontend App",
             "--description", "Customer-facing web application",
             "--template-ids", baseId]);
    }

    if (baseTemplateId is not null && dbTemplateId is not null)
    {
        await RunAsync("Project: Backend API",
            ["project", "create",
             "--name", "Backend API",
             "--description", "Core REST API service",
             "--template-ids", $"{baseId},{dbId}"]);
    }

    if (dbTemplateId is not null)
    {
        await RunAsync("Project: Worker Service",
            ["project", "create",
             "--name", "Worker Service",
             "--description", "Background job processing service",
             "--template-ids", dbId]);
    }
}
else
{
    PrintSkip("Projects (all)", "No templates were created");
}

// ─── Final Summary ────────────────────────────────────────────────────────────

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("  SUMMARY");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.ResetColor();

var createdCount = 0;
var skippedCount = 0;

foreach (var (label, id, created) in summary)
{
    if (created)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.Write($"{label,-48}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(id?.ToString() ?? string.Empty);
        Console.ResetColor();
        createdCount++;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("  ~ ");
        Console.ResetColor();
        Console.WriteLine(label);
        skippedCount++;
    }
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"  Created: {createdCount}  |  Skipped/Failed: {skippedCount}");
Console.ResetColor();
Console.WriteLine();

return 0;

// ─── Helpers ──────────────────────────────────────────────────────────────────

static void PrintHelp()
{
    Console.WriteLine("""
        GroundControl Seed Data

        Usage:
          dotnet tools/seed.cs [-- [options]]

        Options:
          --cli <path>             Path to the groundcontrol CLI executable
                                   (default: groundcontrol, resolved from PATH)
          --server-url <url>       Server URL shown in the banner for reference.
                                   The user must already be logged in via:
                                     groundcontrol auth login --server-url <url>
          --seed-password <pwd>    Password assigned to seeded user accounts
                                   (default: Test!Password123)
          -h, --help               Show this help

        What gets seeded (in dependency order):
          Groups        — Platform Team, Development Team
          Scopes        — Environment (dev/staging/prod), Region, AppTier
          Variables     — DatabaseConnectionString, ApiRateLimit, LogLevel
          Templates     — Base Application Template, Database Template
          Config Entries — app.name/version/debug, db.host/port/name/schema
          Users         — developer, testadmin
          Projects      — Frontend App, Backend API, Worker Service

        Failed creates are non-fatal — the script continues and the step is
        marked as skipped in the summary. Re-running against a server that
        already has the data is safe.
        """);
}

void PrintPhase(string name)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("── ");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write(name);
    Console.ResetColor();
    Console.WriteLine();
}

void PrintSkip(string label, string reason)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine($"  ~ {label}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"    Reason: {reason}");
    Console.ResetColor();
    summary.Add((label, null, false));
}

async Task RunAsync(string label, string[] cliArgs)
{
    Console.Write($"  {label,-50}");

    var (success, stdout, stderr) = await RunCliAsync(cliArgs);

    if (success)
    {
        var id = ExtractId(stdout);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(id.HasValue ? $"✓  {id}" : "✓");
        Console.ResetColor();
        summary.Add((label, id, true));
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("~  skipped");
        Console.ResetColor();
        var reason = FirstLine(stderr.Length > 0 ? stderr : stdout);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    {reason}");
        Console.ResetColor();
        summary.Add((label, null, false));
    }
}

async Task<Guid?> CreateAsync(string label, string[] cliArgs)
{
    Console.Write($"  {label,-50}");

    var (success, stdout, stderr) = await RunCliAsync(cliArgs);

    if (success)
    {
        var id = ExtractId(stdout);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(id.HasValue ? $"✓  {id}" : "✓");
        Console.ResetColor();
        summary.Add((label, id, true));
        return id;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("~  skipped");
        Console.ResetColor();
        var reason = FirstLine(stderr.Length > 0 ? stderr : stdout);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    {reason}");
        Console.ResetColor();
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
    const int maxLen = 110;
    var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? text.Trim();
    return line.Length > maxLen ? line[..maxLen] + "…" : line;
}
