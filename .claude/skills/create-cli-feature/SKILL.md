---
name: create-cli-feature
description: >-
  Create new CLI features (commands, subcommands, handlers) using the GroundControl.Host.Cli
  framework built on System.CommandLine 2.0. Use this skill whenever the user wants to add a new
  command, subcommand, option, argument, or feature to a CLI tool that uses GroundControl.Host.Cli.
  Also activate when: scaffolding a new CLI verb, creating a command handler, adding IShell prompts
  or spinners, setting up CLI dependency injection, configuring options parsing, creating a
  CliHostBuilder entry point, or adding interactive console UI to a CLI tool. If the project has
  command classes using [RootCommand] attributes, this skill applies.
---

# Creating CLI Features with GroundControl.Host.Cli

This skill guides you through adding new features to CLI tools built on the `GroundControl.Host.Cli` framework. The framework wraps System.CommandLine 2.0 and provides conventions for command discovery, dependency injection, and interactive console UI.

Before creating any files, read the existing codebase to understand the project's conventions — look at existing commands, handlers, and dependency modules to match the local style.

## How the Framework Works

`CliHostBuilder` scans the calling assembly for classes decorated with `[RootCommand]` (or `[RootCommand<TDependencyModule>]`). Each discovered class becomes a top-level command. Subcommands are added in the root command's constructor.

When a command is invoked, the framework:
1. Parses the command line and identifies the target command type
2. Extracts handler and dependency module types from the generic parameters
3. Instantiates dependency modules (parameterless constructors) and calls `ConfigureServices`
4. Resolves the handler from DI and calls `HandleAsync`

There are two command variants:
- `Command<THandler, TOption>` — handler + options, no subcommand-specific DI
- `Command<THandler, TOption, TDependencyModule>` — adds a subcommand-level dependency module

### Automatically Registered Services

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IShell` | Singleton | Interactive console UI — use this for **all** user output and prompts |
| `IHttpClientFactory` | — | HTTP client factory via `AddHttpClient()` |
| Handler (`THandler`) | Transient | The command handler itself |

Always use `IShell` for console interaction — never use `IAnsiConsole` or `Console` directly. If you need console functionality that `IShell` doesn't provide, create a new extension method for `IShell` (it doesn't need to live in the same `ShellExtensions` class — any static class with `extension(IShell shell)` works). This keeps all output routed through a single abstraction for testability and consistent styling.

Your dependency modules register additional services.

## Entry Point (Program.cs)

The entry point is a single top-level statement:

```csharp
return await new CliHostBuilder(args, "My CLI tool description")
    .RunAsync();
```

`CliHostBuilder` scans the **calling assembly** for `[RootCommand]`-decorated classes. Pass a specific assembly via the optional third parameter if commands live elsewhere.

### CliHostBuilder Properties

Before calling `Build()` or `RunAsync()`, you can customize the host:

| Property | Type | Purpose |
|----------|------|---------|
| `Configuration` | `ConfigurationManager` | Add configuration sources |
| `Environment` | `IHostEnvironment` | Read environment info |
| `Logging` | `ILoggingBuilder` | Add/configure logging providers |
| `Services` | `IServiceCollection` | Register host-level services (before dependency modules run) |

### Configuration Files

`appsettings.json` and `appsettings.local.json` are automatically loaded from `AppContext.BaseDirectory` (both optional, with reload-on-change). Values are available via `DependencyModuleContext.Configuration` in dependency modules and `IConfiguration` from DI.

### Built-in --debug Option

All CLI apps get a recursive `--debug` option:
- `--debug` — Information-level console logging
- `--debug verbose` (or `--debug v`) — Debug-level console logging
- Without `--debug`, all logging providers are cleared (silent)

## Feature File Organization

Every CLI feature lives under a `Features/` directory and follows this structure:

```
Features/
└── {Feature}/
    ├── {Feature}Command.cs              # Root command with [RootCommand<T>]
    ├── {Feature}DependencyModule.cs      # Shared DI for all subcommands (optional)
    ├── {Feature}CommandOptions.cs        # Shared options POCO (if subcommands share options)
    └── {Verb}/
        ├── {Verb}{Feature}Subcommand.cs          # Static factory class with Create()
        ├── {Verb}{Feature}CommandHandler.cs       # ICommandHandler implementation
        ├── {Verb}{Feature}CommandOptions.cs        # Options POCO (if subcommand has its own)
        └── {Verb}{Feature}DependencyModule.cs     # Subcommand-specific DI (optional)
```

The naming convention puts the verb first in subcommand files (e.g., `CreateProjectSubcommand`, `GenerateReportSubcommand`). Use this to keep related files grouped alphabetically.

## Step-by-Step: Adding a New Feature

### 1. Determine the Command Structure

Decide the command hierarchy. For example, if you're adding a `report` command with a `generate` subcommand, the CLI invocation would be:

```
mycli report generate --template monthly --output ./reports
```

This means:
- **Root command**: `report` (the noun/feature area)
- **Subcommand**: `generate` (the verb/action)

### 2. Create the Root Command

Create `Features/Report/ReportCommand.cs`:

```csharp
using System.CommandLine;

namespace MyCli.Features.Report;

[RootCommand<ReportDependencyModule>]
internal class ReportCommand : Command
{
    public ReportCommand()
        : base("report", "Manage report generation")
    {
        Subcommands.Add(GenerateReportSubcommand.Create());
    }
}
```

Key points:
- Inherits from `System.CommandLine.Command` (not the generic variant)
- Decorated with `[RootCommand]` or `[RootCommand<TDependencyModule>]` for auto-discovery
- Constructor passes the command name and description to the base class
- Subcommands are added in the constructor via their static `Create()` factory methods
- Use `[RootCommand]` (non-generic) if the feature needs no shared DI registrations

### 3. Create the Root Dependency Module (if needed)

Create `Features/Report/ReportDependencyModule.cs` when subcommands share infrastructure services:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MyCli.Features.Report;

internal class ReportDependencyModule : IDependencyModule
{
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        services.AddSingleton<IReportFormatter, ReportFormatter>();
    }
}
```

The `DependencyModuleContext` provides `Environment` (IHostEnvironment) and `Configuration` (IConfiguration) if you need environment-aware registration.

### 4. Create the Subcommand

Create `Features/Report/Generate/GenerateReportSubcommand.cs`:

```csharp
using System.CommandLine;

namespace MyCli.Features.Report.Generate;

internal static class GenerateReportSubcommand
{
    internal static Command<GenerateReportCommandHandler, GenerateReportCommandOptions> Create()
    {
        var templateOption = new Option<string>("--template", "-t")
        {
            Description = "Report template name.",
            Required = true,
            Validators = { FileValidators.NotNullOrWhiteSpace("Template name cannot be empty.") }
        };

        var outputOption = new Option<DirectoryInfo>("--output-path", "-o")
        {
            Description = "Output directory for generated reports.",
            Required = false,
            CustomParser = Parsers.DirectoryInfoParser,
            DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory)
        };

        var command = new Command<GenerateReportCommandHandler, GenerateReportCommandOptions>(
            "generate",
            "Generate a report from a template");

        command.Options.Add(templateOption);
        command.Options.Add(outputOption);

        command.ConfigureOptions((parseResult, options) =>
        {
            options.TemplateName = parseResult.GetRequiredValue(templateOption);
            options.OutputDirectory = parseResult.GetRequiredValue(outputOption).FullName;
        });

        return command;
    }
}
```

The subcommand is always a `static class` with a single `Create()` factory method that returns a `Command<THandler, TOption>` or `Command<THandler, TOption, TDependencyModule>`.

Use the three-type-parameter variant when the subcommand needs its own DI registrations beyond what the root module provides:

```csharp
internal static Command<GenerateReportCommandHandler, GenerateReportCommandOptions, GenerateReportDependencyModule> Create()
```

Read `references/system-commandline-reference.md` for the full System.CommandLine 2.0 option API (validators, custom parsers, default values).

### 5. Create the Options Class

Create `Features/Report/Generate/GenerateReportCommandOptions.cs`:

```csharp
namespace MyCli.Features.Report.Generate;

public sealed class GenerateReportCommandOptions
{
    public required string TemplateName { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;
}
```

Options classes are plain POCOs. Use `required` for properties that must be set. The framework resolves `IOptions<T>` automatically (the Options infrastructure is pre-registered by `Host.CreateApplicationBuilder()`). The `ConfigureOptions` callback on the command populates properties from `ParseResult` — you do not need to call `services.AddOptions<T>()` for this flow to work. Only call `services.Configure<T>()` if you need to bind options to a configuration section.

### 6. Create the Command Handler

Create `Features/Report/Generate/GenerateReportCommandHandler.cs`:

```csharp
namespace MyCli.Features.Report.Generate;

internal class GenerateReportCommandHandler : ICommandHandler
{
    private readonly GenerateReportCommandOptions _options;
    private readonly IReportFormatter _formatter;
    private readonly IShell _shell;

    public GenerateReportCommandHandler(
        IOptions<GenerateReportCommandOptions> options,
        IReportFormatter formatter,
        IShell shell)
    {
        _options = options.Value;
        _formatter = formatter;
        _shell = shell;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        await _shell.ShowStatusAsync("Generating report...", async () =>
        {
            var report = await _formatter.FormatAsync(_options.TemplateName, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(_options.OutputDirectory, "report.html"),
                report,
                cancellationToken);
        });

        _shell.DisplaySuccess($"Report generated at '{_options.OutputDirectory}'.");
        return 0;
    }
}
```

Key points:
- Implements `ICommandHandler` (single method: `HandleAsync`)
- Receives dependencies through constructor injection
- Options come through `IOptions<T>` (not the raw type)
- Use `IShell` for user-facing output (spinners, prompts, success/error messages)

### Error Handling and Exit Codes

- Return `0` for success, non-zero for expected failures
- `TaskCanceledException` and `OperationCanceledException` are caught by the framework and return exit code `0` (graceful cancellation)
- All other unhandled exceptions are logged at `Critical` level and return exit code `1`
- Use `IShell.DisplayError()` for user-facing error messages before returning a non-zero exit code

### 7. Create the Subcommand Dependency Module (if needed)

Create `Features/Report/Generate/GenerateReportDependencyModule.cs` only when the subcommand needs services beyond what the root module provides:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MyCli.Features.Report.Generate;

internal class GenerateReportDependencyModule : IDependencyModule
{
    public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
    {
        services.AddTransient<ITemplateEngine, TemplateEngine>();
    }
}
```

Remember: dependency modules must have a public parameterless constructor — the framework instantiates them via `Activator.CreateInstance`.

## IShell — Interactive Console

`IShell` is automatically available via DI and provides Spectre.Console-based UI. Use `IShell` for **all** user interaction — never use `Console`, `AnsiConsole`, or `IAnsiConsole` directly. If `IShell` doesn't have a method you need, add a new extension method for `IShell` rather than bypassing it (it doesn't need to live in the same `ShellExtensions` class — any static class with `extension(IShell shell)` works).

**Display methods:**

| Method | Purpose |
|--------|---------|
| `DisplaySuccess(message)` | Success with thumbs-up emoji |
| `DisplayError(errorMessage)` | Error in red bold with thumbs-down emoji |
| `DisplayMessage(message)` | Plain text output |
| `DisplayMessage(emoji, message)` | Message prefixed with Spectre emoji shortcode |
| `DisplaySubtleMessage(message)` | Dimmed/secondary text |
| `DisplayEmptyLine()` | Blank line |
| `DisplayException(ex, formats)` | Render exception (default: `ShortenEverything`) |
| `DisplayLines(lines)` | Render `(stream, line)` tuples; stderr in red |

**Progress/Status:**

| Method | Purpose |
|--------|---------|
| `ShowStatus(text, action)` | Sync spinner |
| `ShowStatusAsync(text, action)` | Async spinner (void) |
| `ShowStatusAsync<T>(text, func)` | Async spinner returning `T` |
| `ShowProgress(action)` | Sync progress bar (`Action<ProgressContext>`) |
| `ShowProgressAsync(action)` | Async progress bar (`Func<ProgressContext, Task>`) |

**Prompts** (all async variants accept `CancellationToken`):

| Method | Purpose |
|--------|---------|
| `ConfirmAsync(prompt, defaultValue)` | Yes/no confirmation |
| `PromptForSelectionAsync(prompt, choices, enableSearch)` | Single-choice (strings) |
| `PromptForSelectionAsync<T>(prompt, choices, choiceFormatter, enableSearch)` | Single-choice (typed) |
| `PromptForMultiSelectionAsync<T>(prompt, choices, choiceFormatter, selectedChoices, allowCustomOption)` | Multi-choice |
| `PromptForStringAsync(prompt, default, validator, isOptional)` | Text input with validation |

All prompt methods also have synchronous variants (drop the `Async` suffix, no `CancellationToken`).

These are extension methods on `IShell` (defined via C# 14 `extension(IShell shell)` syntax), not direct interface members. `IShell` itself only exposes `IAnsiConsole Console { get; }`. Selection prompts throw `EmptyChoicesException` when the choices collection is empty.

## Adding a Subcommand to an Existing Root Command

If the root command already exists, you only need:
1. Create the subcommand files in a new `{Verb}/` subdirectory
2. Add the subcommand to the root command's constructor:

```csharp
Subcommands.Add(ExportReportSubcommand.Create());
```

3. If the new subcommand needs its own DI, use the three-type-parameter `Command<,,>` variant

## Common Patterns

### Options with File/Directory Validation

```csharp
var inputOption = new Option<FileInfo>("--input", "-i")
{
    Description = "Path to the input file.",
    Required = true,
    CustomParser = Parsers.FileInfoParser,
    Validators = { FileValidators.FileInfoExists("Input file not found.") }
};
```

### Options with Service Provider Access

When `ConfigureOptions` needs to interact with DI services (e.g., prompting the user via `IShell`):

```csharp
command.ConfigureOptions((parseResult, options, provider) =>
{
    var shell = provider.GetRequiredService<IShell>();
    options.Format = parseResult.GetValue(formatOption)
        ?? shell.PromptForSelection("Select output format:", ["json", "csv", "html"]);
});
```

### Positional Arguments

For positional parameters (no `--` prefix), use `Argument<T>`:

```csharp
var pathArgument = new Argument<string>("path") { Description = "Path to the input file." };
command.Arguments.Add(pathArgument);
```

See `references/system-commandline-reference.md` for full `Argument<T>` API.

### Registering Multiple Implementations

When a subcommand needs multiple implementations of the same interface:

```csharp
services.TryAddEnumerable(
[
    ServiceDescriptor.Transient<IExporter, JsonExporter>(),
    ServiceDescriptor.Transient<IExporter, CsvExporter>(),
    ServiceDescriptor.Transient<IExporter, XmlExporter>()
]);
```

The handler injects `IEnumerable<IExporter>` and iterates through them.

## Checklist

Before you're done, verify:
- [ ] Root command has `[RootCommand]` or `[RootCommand<T>]` attribute
- [ ] Root command inherits from `System.CommandLine.Command` (not the generic variant)
- [ ] Subcommand `Create()` returns the correct `Command<,,>` variant
- [ ] Options properties are populated via `ConfigureOptions` callback using `parseResult.GetRequiredValue()`/`GetValue()`
- [ ] Handler implements `ICommandHandler` and is injected via DI (auto-registered as transient by the framework)
- [ ] All user output goes through `IShell`, not `Console`/`AnsiConsole`/`IAnsiConsole`
- [ ] Required options use `parseResult.GetRequiredValue()`, optional use `parseResult.GetValue()`
- [ ] Dependency modules have parameterless constructors