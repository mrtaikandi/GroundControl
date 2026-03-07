using System.Diagnostics;
using System.Text.Json;

var inputJson = Console.In.ReadToEnd();
if (string.IsNullOrWhiteSpace(inputJson))
{
    return;
}

try
{
    var payloadDocument = JsonDocument.Parse(inputJson);
    using var document = payloadDocument;
    var payload = payloadDocument.RootElement;

    var changedFilePath = GetChangedFilePath(payload);
    if (!File.Exists(changedFilePath))
    {
        Console.Error.WriteLine($"Changed file path does not exist: {changedFilePath}");
        return;
    }

    if (!string.Equals(Path.GetExtension(changedFilePath), ".cs", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var workingDirectory = Path.GetDirectoryName(changedFilePath);
    if (workingDirectory is null)
    {
        return;
    }

    await DotnetFormatAsync(workingDirectory, changedFilePath);
}
catch (JsonException)
{
    Console.Error.WriteLine("Hook received invalid JSON input.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"dotnet format hook failed with exception: {ex.Message}");
}

static string? GetChangedFilePath(JsonElement payload)
{
    if (payload.TryGetProperty("tool_input", out var toolInput) &&
        toolInput.TryGetProperty("file_path", out var filePath) &&
        filePath.ValueKind != JsonValueKind.Null)
    {
        return filePath.GetString();
    }

    if (payload.TryGetProperty("tool_response", out var toolResponse) &&
        toolResponse.TryGetProperty("filePath", out var responseFilePath) &&
        responseFilePath.ValueKind != JsonValueKind.Null)
    {
        return responseFilePath.GetString();
    }

    return null;
}

static async Task DotnetFormatAsync(string workingDirectory, string filePath)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        Arguments = $"format --include {filePath} --no-restore --verbosity minimal"
    };

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        Console.Error.WriteLine("Unable to start dotnet format process.");
        return;
    }

    var standardOutput = process.StandardOutput.ReadToEnd();
    var standardError = process.StandardError.ReadToEnd();

    await process.WaitForExitAsync();

    if (process.ExitCode == 0)
    {
        return;
    }

    var combinedOutput = string.Join(
        Environment.NewLine,
        new[] { standardOutput, standardError }.Where(static text => !string.IsNullOrWhiteSpace(text)));

    Console.Error.WriteLine($"dotnet format failed for '{filePath}': {Environment.NewLine}{combinedOutput}");
}