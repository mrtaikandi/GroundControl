using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Represents a built CLI application ready for execution.
/// </summary>
public sealed partial class CliHost
{
    private readonly IHost? _applicationHost;
    private readonly Type? _commandType;
    private readonly string? _error;
    private readonly ParseResult? _parseResult;

    internal CliHost(ParseResult parseResult) => _parseResult = parseResult;

    internal CliHost(ParseResult parseResult, IHost applicationHost, Type commandType)
    {
        _parseResult = parseResult;
        _applicationHost = applicationHost;
        _commandType = commandType;
    }

    private CliHost(string error) => _error = error;

    /// <summary>
    /// Executes the parsed command and returns the exit code.
    /// </summary>
    /// <returns>The process exit code.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public async Task<int> RunAsync()
    {
        if (_error is not null)
        {
            AnsiConsole.MarkupLine($":thumbs_down:  {_error}");
            return 1;
        }

        Debug.Assert(_parseResult != null, nameof(_parseResult) + " != null");

        if (_applicationHost is null)
        {
            return await _parseResult.InvokeAsync();
        }

        try
        {
            Debug.Assert(_commandType != null, nameof(_commandType) + " != null");
            const BindingFlags BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            var providerField = _commandType.GetProperty(nameof(Command<,>.Provider), BindingFlags)
                                ?? _commandType.BaseType?.GetProperty(nameof(Command<,>.Provider), BindingFlags);

            providerField?.SetValue(_parseResult.CommandResult.Command, _applicationHost.Services);

            var invocationConfiguration = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
            return await _parseResult.InvokeAsync(invocationConfiguration);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            var hostOptions = _applicationHost.Services.GetRequiredService<IOptions<CliHostOptions>>().Value;
            var shell = _applicationHost.Services.GetRequiredService<IShell>();

            shell.DisplayEmptyLine();
            shell.DisplayError("Oops! something went wrong while executing the command.");

            if (hostOptions.Debug)
            {
                shell.DisplayException(ex);

                var logger = _applicationHost.Services.GetRequiredService<ILogger<CliHost>>();
                LogUnhandledError(logger, ex);
            }
            else
            {
                shell.DisplayExceptionSummary(ex);
            }

            shell.DisplayEmptyLine();
            return 1;
        }
    }

    internal static CliHost CreateError(string error) => new(error);

    [LoggerMessage(1, LogLevel.Critical, "An unexpected error occurred while executing the command.")]
    private static partial void LogUnhandledError(ILogger<CliHost> logger, Exception exception);
}