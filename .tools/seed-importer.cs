#!/usr/bin/env dotnet
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:package Spectre.Console@0.55.2

// Meridian Digital Solutions — GroundControl Seed Data Importer
// Reads JSON seed files from a directory and imports them via the groundcontrol CLI.
//
// Usage:
//   dotnet seed-importer.cs -- --data-dir ./seed-data
//   dotnet seed-importer.cs -- --data-dir ./seed-data --dry-run --verbose
//
// JSON file format (files processed in alphabetical order):
// {
//   "scopes":    [ { "$id": "...", "dimension": "...", "values": "...", "description": "..." } ],
//   "groups":    [ { "$id": "...", "name": "...", "description": "..." } ],
//   "templates": [ { "$id": "...", "name": "...", "description": "...", "groupRef": "...", "entries": [...] } ],
//   "projects":  [ { "name": "...", "description": "...", "groupRef": "...", "templateRefs": [...], "entries": [...], "publishSnapshot": true } ]
// }
//
// Config entry values format:
//   "values": [ { "scopes": {}, "value": "default-value" }, { "scopes": { "environment": "prod" }, "value": "prod-value" } ]

using System.Diagnostics;
using System.Text.Json.Nodes;
using Spectre.Console;

// ── Banner ───────────────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new FigletText("GroundControl").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[grey]Seed Data Importer[/]");
AnsiConsole.WriteLine();

// ── Argument parsing ─────────────────────────────────────────────────────────
var dataDir = ".";
var cliCommand = "groundcontrol";
var dryRun = false;
var verbose = false;
var stopOnError = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--data-dir":
        case "-d":
            if (++i < args.Length) dataDir = args[i];
            break;
        case "--command":
            if (++i < args.Length) cliCommand = args[i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        case "--verbose":
        case "-v":
            verbose = true;
            break;
        case "--stop-on-error":
            stopOnError = true;
            break;
        case "--help":
        case "-h":
            PrintHelp();
            return 0;
    }
}

if (!Directory.Exists(dataDir))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] Data directory not found: [yellow]{Path.GetFullPath(dataDir)}[/]");
    AnsiConsole.MarkupLine("[grey]Use --data-dir <path> to specify the directory containing JSON seed files.[/]");
    return 1;
}

var settingsTable = new Table().NoBorder().HideHeaders().AddColumns("k", "v");
settingsTable.AddRow("[grey]CLI command[/]", $"[white]{cliCommand}[/]");
settingsTable.AddRow("[grey]Data dir   [/]", $"[white]{Path.GetFullPath(dataDir)}[/]");
settingsTable.AddRow("[grey]Dry run    [/]", dryRun ? "[yellow]yes (nothing will be created)[/]" : "[green]no[/]");
settingsTable.AddRow("[grey]Verbose    [/]", verbose ? "[green]yes[/]" : "[grey]no[/]");
AnsiConsole.Write(settingsTable);
AnsiConsole.WriteLine();

// ── Discover seed files ───────────────────────────────────────────────────────
var jsonFiles = Directory.GetFiles(dataDir, "*.json")
    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (jsonFiles.Length == 0)
{
    AnsiConsole.MarkupLine($"[red]No JSON files found in '[yellow]{dataDir}[/]'.[/]");
    return 1;
}

AnsiConsole.MarkupLine($"Found [bold]{jsonFiles.Length}[/] seed file(s) to process.");
AnsiConsole.WriteLine();

// ── State ─────────────────────────────────────────────────────────────────────
var idMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
var stats = new ImportStats();
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ── Main processing loop ──────────────────────────────────────────────────────
foreach (var file in jsonFiles)
{
    if (cts.Token.IsCancellationRequested)
    {
        AnsiConsole.MarkupLine("[yellow]Import cancelled by user.[/]");
        break;
    }

    AnsiConsole.Write(new Rule($"[bold]{Path.GetFileName(file)}[/]").LeftJustified().RuleStyle("grey"));

    JsonNode? root;
    try
    {
        var content = await File.ReadAllTextAsync(file, cts.Token);
        root = JsonNode.Parse(content, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Failed to read '[yellow]{file}[/]': {ex.Message.EscapeMarkup()}[/]");
        if (stopOnError) return 1;
        stats.Errors++;
        continue;
    }

    if (root is not JsonObject rootObj) continue;

    // Process in dependency order: scopes → groups → templates → projects
    if (rootObj["scopes"] is JsonArray scopes)
    {
        foreach (var scope in scopes.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessScopeAsync(scope);
            if (stopOnError && stats.Errors > 0) return 1;
        }
    }

    if (rootObj["groups"] is JsonArray groups)
    {
        foreach (var group in groups.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessGroupAsync(group);
            if (stopOnError && stats.Errors > 0) return 1;
        }
    }

    if (rootObj["templates"] is JsonArray templates)
    {
        foreach (var template in templates.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessTemplateAsync(template);
            if (stopOnError && stats.Errors > 0) return 1;
        }
    }

    if (rootObj["projects"] is JsonArray projects)
    {
        foreach (var project in projects.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessProjectAsync(project);
            if (stopOnError && stats.Errors > 0) return 1;
        }
    }

    AnsiConsole.WriteLine();
}

// ── Summary ───────────────────────────────────────────────────────────────────
var success = stats.Errors == 0;
var summaryTitle = success ? "[green]Import completed successfully[/]" : "[red]Import completed with errors[/]";

var summary = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(success ? Color.Green : Color.Red)
    .AddColumn(new TableColumn("[grey]Entity[/]"))
    .AddColumn(new TableColumn("[grey]Created[/]").RightAligned());

summary.AddRow("Scopes",    $"[white]{stats.Scopes}[/]");
summary.AddRow("Groups",    $"[white]{stats.Groups}[/]");
summary.AddRow("Templates", $"[white]{stats.Templates}[/]");
summary.AddRow("Projects",  $"[white]{stats.Projects}[/]");
summary.AddRow("Entries",   $"[white]{stats.Entries}[/]");
summary.AddRow("Snapshots", $"[white]{stats.Snapshots}[/]");

if (stats.Errors > 0)
    summary.AddRow("[red]Errors[/]", $"[red]{stats.Errors}[/]");

AnsiConsole.Write(new Rule(summaryTitle));
AnsiConsole.Write(summary);
AnsiConsole.WriteLine();

return stats.Errors > 0 ? 1 : 0;

// ─────────────────────────────────────────────────────────────────────────────
// Processor methods
// ─────────────────────────────────────────────────────────────────────────────

async Task ProcessScopeAsync(JsonObject scope)
{
    var localId = scope["$id"]?.GetValue<string>();
    var dimension = GetString(scope, "dimension");
    var values = GetString(scope, "values");
    var description = GetString(scope, "description");

    if (dimension is null || values is null)
    {
        AnsiConsole.MarkupLine("  [red][[scope]][/] Skipping entry: missing 'dimension' or 'values'.");
        stats.Errors++;
        return;
    }

    AnsiConsole.Markup($"  [steelblue1][[scope]][/]    {dimension.EscapeMarkup(),-40}");

    var cliArgs = BuildArgs("scope", "create",
        "--dimension", dimension,
        "--values", values,
        "--output", "json",
        "--no-interactive");

    if (description is not null) cliArgs.AddRange(["--description", description]);

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    HandleResult(exitCode, stdout, stderr, localId, ref stats.Scopes);
}

async Task ProcessGroupAsync(JsonObject group)
{
    var localId = group["$id"]?.GetValue<string>();
    var name = GetString(group, "name");
    var description = GetString(group, "description");

    if (name is null)
    {
        AnsiConsole.MarkupLine("  [red][[group]][/] Skipping entry: missing 'name'.");
        stats.Errors++;
        return;
    }

    AnsiConsole.Markup($"  [steelblue1][[group]][/]    {name.EscapeMarkup(),-40}");

    var cliArgs = BuildArgs("group", "create",
        "--name", name,
        "--output", "json",
        "--no-interactive");

    if (description is not null) cliArgs.AddRange(["--description", description]);

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    HandleResult(exitCode, stdout, stderr, localId, ref stats.Groups);
}

async Task ProcessTemplateAsync(JsonObject template)
{
    var localId = template["$id"]?.GetValue<string>();
    var name = GetString(template, "name");
    var description = GetString(template, "description");
    var groupRef = GetString(template, "groupRef");

    if (name is null)
    {
        AnsiConsole.MarkupLine("  [red][[template]][/] Skipping entry: missing 'name'.");
        stats.Errors++;
        return;
    }

    AnsiConsole.Markup($"  [steelblue1][[template]][/] {name.EscapeMarkup(),-40}");

    var cliArgs = BuildArgs("template", "create",
        "--name", name,
        "--output", "json",
        "--no-interactive");

    if (description is not null) cliArgs.AddRange(["--description", description]);
    if (groupRef is not null && idMap.TryGetValue(groupRef, out var gid))
        cliArgs.AddRange(["--group-id", gid.ToString()]);

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    var templateId = HandleResult(exitCode, stdout, stderr, localId, ref stats.Templates);

    if (exitCode != 0 || templateId == Guid.Empty) return;

    if (template["entries"] is JsonArray entries)
    {
        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessConfigEntryAsync(entry, templateId, "Template");
            if (stopOnError && stats.Errors > 0) return;
        }
    }
}

async Task ProcessProjectAsync(JsonObject project)
{
    var name = GetString(project, "name");
    var description = GetString(project, "description");
    var groupRef = GetString(project, "groupRef");
    var publishSnapshot = project["publishSnapshot"]?.GetValue<bool>() ?? false;

    if (name is null)
    {
        AnsiConsole.MarkupLine("  [red][[project]][/] Skipping entry: missing 'name'.");
        stats.Errors++;
        return;
    }

    AnsiConsole.Markup($"  [steelblue1][[project]][/]  {name.EscapeMarkup(),-40}");

    var cliArgs = BuildArgs("project", "create",
        "--name", name,
        "--output", "json",
        "--no-interactive");

    if (description is not null) cliArgs.AddRange(["--description", description]);
    if (groupRef is not null && idMap.TryGetValue(groupRef, out var gid)) cliArgs.AddRange(["--group-id", gid.ToString()]);

    if (project["templateRefs"] is JsonArray templateRefs)
    {
        var resolvedIds = templateRefs
            .OfType<JsonValue>()
            .Select(r => r.GetValue<string>())
            .Where(r => idMap.ContainsKey(r))
            .Select(r => idMap[r].ToString())
            .ToList();

        if (resolvedIds.Count > 0) cliArgs.AddRange(["--template-ids", string.Join(",", resolvedIds)]);
    }

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    var projectId = HandleResult(exitCode, stdout, stderr, null, ref stats.Projects);

    if (exitCode != 0 || projectId == Guid.Empty) return;

    if (project["entries"] is JsonArray entries)
    {
        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (cts.Token.IsCancellationRequested) break;
            await ProcessConfigEntryAsync(entry, projectId, "Project");
            if (stopOnError && stats.Errors > 0) return;
        }
    }

    if (publishSnapshot) await PublishSnapshotAsync(projectId, name);
}

async Task ProcessConfigEntryAsync(JsonObject entry, Guid ownerId, string ownerType)
{
    var key = GetString(entry, "key");
    var valueType = GetString(entry, "valueType") ?? "String";
    var description = GetString(entry, "description");
    var sensitive = entry["sensitive"]?.GetValue<bool>() ?? false;

    if (key is null)
    {
        AnsiConsole.MarkupLine("    [red][[entry]][/] Skipping: missing 'key'.");
        stats.Errors++;
        return;
    }

    AnsiConsole.Markup($"    [grey][[entry]][/]    {key.EscapeMarkup(),-38}");

    var cliArgs = BuildArgs("config-entry", "create",
        "--key", key,
        "--owner-id", ownerId.ToString(),
        "--owner-type", ownerType,
        "--value-type", valueType,
        "--output", "json",
        "--no-interactive");

    if (sensitive) cliArgs.AddRange(["--sensitive", "true"]);
    if (description is not null) cliArgs.AddRange(["--description", description]);

    if (entry["values"] is JsonArray valuesArr && valuesArr.Count > 0) cliArgs.AddRange(["--values-json", valuesArr.ToJsonString()]);

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    HandleResult(exitCode, stdout, stderr, null, ref stats.Entries);
}

async Task PublishSnapshotAsync(Guid projectId, string projectName)
{
    AnsiConsole.Markup($"    [grey][[snapshot]][/] {projectName.EscapeMarkup(),-38}");

    var cliArgs = BuildArgs("snapshot", "publish",
        "--project-id", projectId.ToString(),
        "--output", "json",
        "--no-interactive");

    var (exitCode, stdout, stderr) = await RunCliAsync(cliArgs);
    HandleResult(exitCode, stdout, stderr, null, ref stats.Snapshots);
}

// ─────────────────────────────────────────────────────────────────────────────
// Infrastructure helpers
// ─────────────────────────────────────────────────────────────────────────────

async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(List<string> cliArgs)
{
    if (dryRun)
    {
        if (verbose)
        {
            var preview = string.Join(" ", cliArgs.Select(a => a.Contains(' ') || a.Contains('"') ? $"\"{a.Replace("\"", "\\\"")}\"" : a));
            AnsiConsole.MarkupLine($"\n    [yellow][[dry-run]][/] {cliCommand.EscapeMarkup()} {preview.EscapeMarkup()}");
        }
        // Return a fake successful response with a zero GUID
        return (0, """{"id":"00000000-0000-0000-0000-000000000001"}""", "");
    }

    if (verbose)
    {
        var preview = string.Join(" ", cliArgs.Select(a => a.Contains(' ') || a.Contains('"') ? $"\"{a.Replace("\"", "\\\"")}\"" : a));
        AnsiConsole.MarkupLine($"\n    [grey]> {cliCommand.EscapeMarkup()} {preview.EscapeMarkup()}[/]");
    }

    var psi = new ProcessStartInfo(cliCommand)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    foreach (var arg in cliArgs)
    {
        psi.ArgumentList.Add(arg);
    }

    Process process;
    try
    {
        process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{cliCommand}'");
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [red]Failed to start '[yellow]{cliCommand.EscapeMarkup()}[/]': {ex.Message.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine("  [grey]Ensure the groundcontrol CLI is installed: dotnet tool install -g GroundControl.Cli[/]");
        return (-1, "", ex.Message);
    }

    var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
    var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);
    await Task.WhenAll(stdOutTask, stdErrTask);
    await process.WaitForExitAsync(cts.Token);

    return (process.ExitCode, stdOutTask.Result, stdErrTask.Result);
}

Guid HandleResult(int exitCode, string stdout, string stderr, string? localId, ref int counter)
{
    // The CLI exits with code 0 even on errors (Program.cs does not propagate the return value).
    // Detect failures by checking whether stdout contains a valid JSON response object.
    var json = ParseJsonObject(stdout);

    if (exitCode != 0 || json is null)
    {
        AnsiConsole.MarkupLine("[red]FAILED[/]");
        var errorText = (stdout.Trim() + " " + stderr.Trim()).Trim();
        if (errorText.Length > 0)
            AnsiConsole.MarkupLine($"    [red]Error:[/] {errorText[..Math.Min(errorText.Length, 200)].EscapeMarkup()}");
        stats.Errors++;
        return Guid.Empty;
    }

    var actualId = Guid.Empty;

    if (json["id"] is JsonValue idValue && Guid.TryParse(idValue.GetValue<string>(), out var parsedId))
        actualId = parsedId;

    if (localId is not null && actualId != Guid.Empty)
        idMap[localId] = actualId;

    counter++;

    if (verbose)
        AnsiConsole.MarkupLine($"[green]OK[/]  [grey](id: {actualId})[/]");
    else
        AnsiConsole.MarkupLine("[green]OK[/]");

    return actualId;
}

static List<string> BuildArgs(params string[] parts) => new(parts);

static string? GetString(JsonObject obj, string key) =>
    obj[key]?.GetValue<string>() is { Length: > 0 } s ? s : null;

static JsonObject? ParseJsonObject(string text)
{
    // Extract the JSON object portion, skipping any leading status messages or ANSI codes.
    var start = text.IndexOf('{');
    var end = text.LastIndexOf('}');
    if (start < 0 || end < start) return null;
    try { return JsonNode.Parse(text[start..(end + 1)]) as JsonObject; }
    catch { return null; }
}

static void PrintHelp()
{
    AnsiConsole.MarkupLine("[bold]Meridian Digital Solutions — GroundControl Seed Data Importer[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Usage:[/]");
    AnsiConsole.MarkupLine("  dotnet seed-importer.cs -- [options]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Options:[/]");
    AnsiConsole.MarkupLine("  [green]--data-dir, -d[/] [grey]<path>[/]    Directory containing *.json seed files  [grey](default: .)[/]");
    AnsiConsole.MarkupLine("  [green]--command[/] [grey]<cmd>[/]          groundcontrol CLI executable name        [grey](default: groundcontrol)[/]");
    AnsiConsole.MarkupLine("  [green]--dry-run[/]                Preview all operations without executing them");
    AnsiConsole.MarkupLine("  [green]--verbose, -v[/]           Print each CLI command before execution");
    AnsiConsole.MarkupLine("  [green]--stop-on-error[/]         Halt import on the first error");
    AnsiConsole.MarkupLine("  [green]--help, -h[/]              Show this help message");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  dotnet seed-importer.cs -- --data-dir ./seed-data");
    AnsiConsole.MarkupLine("  dotnet seed-importer.cs -- --data-dir ./seed-data --dry-run --verbose");
    AnsiConsole.MarkupLine("  dotnet seed-importer.cs -- --data-dir ./seed-data --stop-on-error");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]JSON file format[/] [grey](files are processed in alphabetical order):[/]");
    AnsiConsole.MarkupLine("  {");
    AnsiConsole.MarkupLine("    [grey]\"scopes\":[/]    [ { [grey]\"$id\"[/]: \"env\", [grey]\"dimension\"[/]: \"Environment\", [grey]\"values\"[/]: \"dev,staging,prod\" } ],");
    AnsiConsole.MarkupLine("    [grey]\"groups\":[/]    [ { [grey]\"$id\"[/]: \"grp-backend\", [grey]\"name\"[/]: \"Backend\" } ],");
    AnsiConsole.MarkupLine("    [grey]\"templates\":[/] [ { [grey]\"$id\"[/]: \"tmpl-svc\", [grey]\"name\"[/]: \"Microservice\", [grey]\"groupRef\"[/]: \"grp-backend\", [grey]\"entries\"[/]: [...] } ],");
    AnsiConsole.MarkupLine("    [grey]\"projects\":[/]  [ { [grey]\"name\"[/]: \"OrderService\", [grey]\"groupRef\"[/]: \"grp-backend\", [grey]\"templateRefs\"[/]: [\"tmpl-svc\"], [grey]\"entries\"[/]: [...], [grey]\"publishSnapshot\"[/]: true } ]");
    AnsiConsole.MarkupLine("  }");
}

// ─────────────────────────────────────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────────────────────────────────────

sealed class ImportStats
{
    public int Scopes;
    public int Groups;
    public int Templates;
    public int Projects;
    public int Entries;
    public int Snapshots;
    public int Errors;
}
