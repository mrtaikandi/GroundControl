using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GroundControl.Host.Cli;

/// <summary>
/// Represents a command handler that executes a command with a specific handler type and options type.
/// </summary>
/// <typeparam name="THandler">The command handler type that implements <see cref="ICommandHandler"/>.</typeparam>
/// <typeparam name="TOption">The options type associated with this command.</typeparam>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicProperties)]
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented", Justification = "It is by design.")]
public class Command<THandler, TOption> : Command
    where THandler : class, ICommandHandler
    where TOption : class
{
    internal IServiceProvider Provider { get; set; } = null!;

    private Action<ParseResult, TOption, IServiceProvider>? _configureOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="Command{THandler, TOption}"/> class.
    /// </summary>
    /// <param name="name">The name of the command.</param>
    /// <param name="description">An optional description of the command.</param>
    public Command(string name, string? description = null)
        : base(name, description)
    {
        SetAction(ExecuteAsync);
    }

    /// <summary>
    /// Configures the command's typed options based on the parsed result.
    /// </summary>
    /// <param name="configureOptions">An action that takes the parse result and options instance to configure the options.</param>
    /// <remarks>
    /// If set, this action will be invoked with the parse result and resolved options instance
    /// before the command handler is executed.
    /// </remarks>
    public void ConfigureOptions(Action<ParseResult, TOption> configureOptions) =>
        _configureOptions = (parseResult, options, _) => configureOptions(parseResult, options);

    /// <summary>
    /// Configures the command's typed options based on the parsed result.
    /// </summary>
    /// <param name="configureOptions">An action that takes the parse result, options instance, and service provider to configure the options.</param>
    /// <remarks>
    /// If set, this action will be invoked with the parse result and resolved options instance
    /// before the command handler is executed.
    /// </remarks>
    public void ConfigureOptions(Action<ParseResult, TOption, IServiceProvider> configureOptions) =>
        _configureOptions = configureOptions;

    private async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Debug.Assert(Provider is not null, "Service provider has not been set.");
        var handler = Provider.GetRequiredService<THandler>();

        if (_configureOptions is not null)
        {
            var options = Provider.GetRequiredService<IOptions<TOption>>();
            _configureOptions(parseResult, options.Value, Provider);
        }

        return await handler.HandleAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a command handler that executes a command with a specific handler type, options type, and its own dependency module.
/// </summary>
/// <typeparam name="THandler">The command handler type that implements <see cref="ICommandHandler"/>.</typeparam>
/// <typeparam name="TOption">The options type associated with this command.</typeparam>
/// <typeparam name="TDependencyModule">The dependency module type used to register additional IoC services for this command.</typeparam>
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented", Justification = "It is by design.")]
public class Command<THandler, TOption, TDependencyModule>(string name, string? description = null)
    : Command<THandler, TOption>(name, description)
    where THandler : class, ICommandHandler
    where TOption : class
    where TDependencyModule : IDependencyModule;