using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using GroundControl.Host.Cli.Internals.IO;
using GroundControl.Host.Cli.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Builds and configures a CLI application host with command discovery, dependency injection,
/// and the standard CapitalOnTap CLI conventions.
/// </summary>
public sealed class CliHostBuilder
{
    private readonly string[] _args;
    private readonly Assembly _commandAssembly;
    private readonly string _description;
    private readonly HostApplicationBuilder _innerBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliHostBuilder" /> class.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="description">The root command description displayed in help text.</param>
    /// <param name="commandAssembly">The assembly to scan for <see cref="RootCommandAttribute" />-decorated commands. If null, the calling assembly is used.</param>
    public CliHostBuilder(string[] args, string description, Assembly? commandAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(args);

        Console.OutputEncoding = Encoding.UTF8;

        _description = description;
        _args = args;
        _commandAssembly = commandAssembly ?? Assembly.GetCallingAssembly();
        _innerBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        ConfigureConfiguration(_innerBuilder.Configuration);
    }

    /// <summary>
    /// Gets the configuration manager for adding configuration sources.
    /// </summary>
    public ConfigurationManager Configuration => _innerBuilder.Configuration;

    /// <summary>
    /// Gets the host environment information.
    /// </summary>
    public IHostEnvironment Environment => _innerBuilder.Environment;

    /// <summary>
    /// Gets the logging builder for configuring logging providers.
    /// </summary>
    public ILoggingBuilder Logging => _innerBuilder.Logging;

    /// <summary>
    /// Gets the service collection for registering additional dependencies.
    /// </summary>
    public IServiceCollection Services => _innerBuilder.Services;

    /// <summary>
    /// Builds the CLI host, discovering commands, wiring dependency injection, and preparing for execution.
    /// </summary>
    /// <returns>A <see cref="CliHost" /> ready to run.</returns>
    public CliHost Build()
    {
        var parseResult = ParseCommandLine();
        ConfigureLogging(_innerBuilder.Logging, parseResult);
        var commandType = parseResult.CommandResult.Command.GetType();

        if (!TryExtractHandlerType(commandType, out var handlerType, out var subCommandModuleType))
        {
            // Commands with no handlers can be executed directly without building the application host.
            return new CliHost(parseResult);
        }

        var result = TryCreateDependencyModules(
            parseResult.CommandResult.Command,
            subCommandModuleType,
            out var rootModule,
            out var subCommandModule);

        if (result.Failed)
        {
            return CliHost.CreateError(result.Error);
        }

        RegisterCommonServices(parseResult, handlerType, rootModule, subCommandModule);

        var app = _innerBuilder.Build();
        return new CliHost(parseResult, app, commandType);
    }

    /// <summary>
    /// Builds and runs the CLI application, returning the exit code.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync()
    {
        var host = Build();
        return await host.RunAsync();
    }

    private ParseResult ParseCommandLine()
    {
        var rootCommand = new RootCommand(_description)
        {
            CliHostOptions.DebugOption,
            CliHostOptions.OutputOption,
            CliHostOptions.NoInteractiveOption
        };

        foreach (var command in DiscoverRootCommands(_commandAssembly))
        {
            rootCommand.Subcommands.Add(command);
        }

        return rootCommand.Parse(_args, new ParserConfiguration { EnablePosixBundling = true });
    }

    private static bool TryExtractHandlerType(
        Type commandType,
        [NotNullWhen(true)] out Type? handlerType,
        out Type? subCommandModuleType)
    {
        handlerType = null;
        subCommandModuleType = null;

        var type = commandType;
        while (type is not null)
        {
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(Command<,>))
                {
                    handlerType = type.GetGenericArguments()[0];
                    break;
                }

                if (genericDef == typeof(Command<,,>))
                {
                    var genericArgs = type.GetGenericArguments();
                    handlerType = genericArgs[0];
                    subCommandModuleType = genericArgs[2];
                    break;
                }
            }

            type = type.BaseType;
        }

        return handlerType is not null;
    }

    private static Result TryCreateDependencyModules(
        Command command,
        Type? subCommandModuleType,
        out IDependencyModule? rootModule,
        out IDependencyModule? subCommandModule)
    {
        rootModule = null;
        subCommandModule = null;

        if (!TryFindRootCommandAttribute(command, out var attribute))
        {
            return Result.Failure(
                "No root command found. Ensure that your command hierarchy " +
                "has a root command decorated with the [RootCommand] attribute.");
        }

        var rootModuleType = attribute.GetType().GetGenericArguments().FirstOrDefault();

        var result = TryCreateModule(rootModuleType, out rootModule);
        return result ? TryCreateModule(subCommandModuleType, out subCommandModule) : result;
    }

    private static Result TryCreateModule(Type? moduleType, out IDependencyModule? module)
    {
        module = null;
        if (moduleType is null)
        {
            return Result.Success;
        }

        if (Activator.CreateInstance(moduleType) is IDependencyModule m)
        {
            module = m;
            return Result.Success;
        }

        return Result.Failure(
            $"Failed to create an instance of the dependency module type '{moduleType.FullName}'. " +
            $"Ensure that it has a public parameterless constructor and implements IDependencyModule.");
    }

    private void RegisterCommonServices(
        ParseResult parseResult,
        Type handlerType,
        IDependencyModule? rootModule,
        IDependencyModule? subCommandModule)
    {
        _innerBuilder.Services.TryAddTransient(handlerType);
        _innerBuilder.Services.AddSingleton(BuildAnsiConsole);
        _innerBuilder.Services.AddSingleton<IShell, Shell>();
        _innerBuilder.Services.AddSingleton<IFileService, FileService>();
        _innerBuilder.Services.AddSingleton<IPackageService, PackageService>();
        _innerBuilder.Services.AddHttpClient();

        ConfigureCliHostOptions(parseResult);

        var dependencyContext = new DependencyModuleContext(_innerBuilder.Environment, _innerBuilder.Configuration);
        rootModule?.ConfigureServices(dependencyContext, _innerBuilder.Services);
        subCommandModule?.ConfigureServices(dependencyContext, _innerBuilder.Services);
    }

    private void ConfigureCliHostOptions(ParseResult parseResult)
    {
        var outputFormat = parseResult.GetValue(CliHostOptions.OutputOption);
        var noInteractive = parseResult.GetValue(CliHostOptions.NoInteractiveOption);

        _innerBuilder.Services.Configure<CliHostOptions>(options =>
        {
            options.OutputFormat = outputFormat;
            options.NoInteractive = noInteractive;
        });
    }

    private static IAnsiConsole BuildAnsiConsole(IServiceProvider serviceProvider)
    {
        var settings = new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            Interactive = InteractionSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect
        };

        return AnsiConsole.Create(settings);
    }

    private static void ConfigureConfiguration(ConfigurationManager configuration)
    {
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), true, true);
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"), true, true);
    }

    private static void ConfigureLogging(ILoggingBuilder builder, ParseResult parseResult)
    {
        builder.ClearProviders();

        if (parseResult.GetResult(CliHostOptions.DebugOption) is null)
        {
            return;
        }

        builder.ClearProviders().AddSpectreConsole();

        var debugValue = parseResult.GetValue(CliHostOptions.DebugOption);
        if (string.Equals(debugValue, "verbose", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(debugValue, "v", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddFilter<SpectreConsoleLoggerProvider>(level => level >= LogLevel.Debug);
        }
        else
        {
            builder.Services.Configure<LoggerFilterOptions>(options =>
            {
                // Only add default rules if isn't provided by appsettings.
                if (options.Rules.Count == 0)
                {
                    options.AddFilter<SpectreConsoleLoggerProvider>("Default", l => l >= LogLevel.Information);
                    options.AddFilter<SpectreConsoleLoggerProvider>("System", l => l >= LogLevel.Warning);
                    options.AddFilter<SpectreConsoleLoggerProvider>("Microsoft", l => l >= LogLevel.Warning);
                    options.AddFilter<SpectreConsoleLoggerProvider>(
                        "Microsoft.Hosting",
                        l => l >= LogLevel.Information);

                    options.AddFilter<SpectreConsoleLoggerProvider>("System.Net.Http", l => l >= LogLevel.Warning);
                }
            });
        }
    }

    private static IEnumerable<Command> DiscoverRootCommands(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types
            .Where(t => t is { IsInterface: false, IsAbstract: false } && typeof(Command).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<RootCommandAttribute>() is not null)
            .Select(t => (Command)Activator.CreateInstance(t)!);
    }

    private static bool TryFindRootCommandAttribute(
        Command command,
        [NotNullWhen(true)] out RootCommandAttribute? attribute)
    {
        var current = command;
        while (current is not null)
        {
            var attr = current.GetType().GetCustomAttribute<RootCommandAttribute>();
            if (attr is not null)
            {
                attribute = attr;
                return true;
            }

            current = current.Parents.OfType<Command>().FirstOrDefault();
        }

        attribute = null;
        return false;
    }

    private readonly record struct Result(string? Error)
    {
        public static readonly Result Success = new(null);

        [MemberNotNullWhen(true, nameof(Error))]
        public bool Failed => Error is not null;

        public static Result Failure(string error) => new(error);

        public static implicit operator bool(Result result) => result.Error is null;
    }
}