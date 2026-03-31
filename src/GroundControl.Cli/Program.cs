
var effectiveArgs = args.Length == 0 ? ["tui"] : args;
return await new CliHostBuilder(effectiveArgs, "GroundControl management tool").RunAsync();