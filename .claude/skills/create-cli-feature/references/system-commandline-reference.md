# System.CommandLine 2.0 API Reference

This reference covers the System.CommandLine 2.0 API as used by the GroundControl.Host.Cli framework. Version 2.0 uses property initializer syntax for options and arguments — not the older fluent builder API from beta versions.

## Option<T>

Options are created using object initializer syntax:

```csharp
var nameOption = new Option<string>("--name", "-n")
{
    Description = "The name of the resource.",
    Required = true,
};
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Description` | `string?` | Help text shown in `--help` output |
| `Required` | `bool` | Whether the option must be provided (default: `false`) |
| `DefaultValueFactory` | `Func<ArgumentResult, T>` | Factory that provides a default value when the option is not specified |
| `CustomParser` | `Func<ArgumentResult, T>` | Custom parsing logic to convert the string token into `T` |
| `Validators` | Collection of `Action<OptionResult>` | Validation actions; call `result.AddError(message)` to signal failure |
| `Recursive` | `bool` | Whether the option is inherited by subcommands |
| `Hidden` | `bool` | Whether the option is hidden from help output |

### Constructor Overloads

```csharp
// Long name only
new Option<string>("--name")

// Long name with alias
new Option<string>("--name", "-n")

// Multiple aliases
new Option<string>("--name", "-n", "--resource-name")
```

### DefaultValueFactory

Provide a default value when the user doesn't supply the option:

```csharp
var outputOption = new Option<DirectoryInfo>("--output-path", "-o")
{
    Description = "Output directory. Defaults to current directory.",
    DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory)
};
```

### CustomParser

Transform the raw string token into a typed value:

```csharp
var fileOption = new Option<FileInfo>("--input", "-i")
{
    CustomParser = Parsers.FileInfoParser  // Framework-provided parser
};
```

The framework provides these parsers in the `Parsers` static class:
- `Parsers.FileInfoParser` — converts string token to `FileInfo`
- `Parsers.DirectoryInfoParser` — converts string token to `DirectoryInfo`

### Validators

Add validation functions to the `Validators` collection:

```csharp
var nameOption = new Option<string>("--name", "-n")
{
    Required = true,
    Validators = { FileValidators.NotNullOrWhiteSpace("Name cannot be empty.") }
};

var inputOption = new Option<FileInfo>("--input", "-i")
{
    CustomParser = Parsers.FileInfoParser,
    Validators = { FileValidators.FileInfoExists("Input file not found.") }
};
```

The framework provides these validators in the `FileValidators` static class:
- `FileValidators.NotNullOrWhiteSpace(errorMessage)` — validates non-empty string
- `FileValidators.FileExists(errorMessage)` — validates file exists at string path
- `FileValidators.FileInfoExists(errorMessage)` — validates `FileInfo.Exists`

Custom validators follow the same `Action<OptionResult>` pattern — call `result.AddError()` to signal failure:

```csharp
Action<OptionResult> validatePositive = result =>
{
    var value = result.GetValueOrDefault<int>();
    if (value <= 0)
    {
        result.AddError("Value must be positive.");
    }
};

var countOption = new Option<int>("--count")
{
    Validators = { validatePositive }
};
```

Multiple validators can be added:

```csharp
Validators =
{
    FileValidators.NotNullOrWhiteSpace("Cannot be empty."),
    validateAlphanumeric
}
```

## Argument<T>

Arguments are positional (no `--` prefix). Same initializer syntax as options:

```csharp
var pathArgument = new Argument<string>("path")
{
    Description = "Path to the input file."
};

command.Arguments.Add(pathArgument);
```

## Adding Options and Arguments to Commands

```csharp
command.Options.Add(nameOption);
command.Options.Add(outputOption);
command.Arguments.Add(pathArgument);
```

## ParseResult — Reading Values

In `ConfigureOptions`, use the `ParseResult` to read parsed values:

```csharp
command.ConfigureOptions((parseResult, options) =>
{
    // Required value — throws if not provided
    options.Name = parseResult.GetRequiredValue(nameOption);

    // Optional value — returns default(T) if not provided
    options.Format = parseResult.GetValue(formatOption) ?? "json";
});
```

| Method | Behavior |
|--------|----------|
| `parseResult.GetRequiredValue(option)` | Returns the value; throws if missing |
| `parseResult.GetValue(option)` | Returns the value or `default(T)` if not provided |

## Subcommands

Subcommands are added to a parent command's `Subcommands` collection:

```csharp
public MyCommand() : base("parent", "Parent command description")
{
    Subcommands.Add(ChildSubcommand.Create());
    Subcommands.Add(OtherSubcommand.Create());
}
```

## Common Option Type Examples

```csharp
// String option
new Option<string>("--name", "-n") { Required = true }

// Boolean flag (no value needed)
new Option<bool>("--force", "-f") { Description = "Force overwrite." }

// Nullable string (optional with no default)
new Option<string?>("--namespace", "-ns") { Required = false }

// FileInfo with parser and validation
new Option<FileInfo>("--input", "-i")
{
    Required = true,
    CustomParser = Parsers.FileInfoParser,
    Validators = { FileValidators.FileInfoExists("File not found.") }
}

// DirectoryInfo with default value
new Option<DirectoryInfo>("--output", "-o")
{
    CustomParser = Parsers.DirectoryInfoParser,
    DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory)
}

// Enum option
new Option<OutputFormat>("--format") { DefaultValueFactory = _ => OutputFormat.Json }
```