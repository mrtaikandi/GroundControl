using System.Text.Json;
using System.Text.Json.Nodes;

namespace GroundControl.Cli.Shared.Config;

/// <summary>
/// Reads and writes the <c>GroundControl</c> configuration section in <c>appsettings.local.json</c>.
/// </summary>
internal sealed class CredentialStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialStore"/> class.
    /// </summary>
    /// <param name="settingsPath">The full path to the <c>appsettings.local.json</c> file.</param>
    public CredentialStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    /// <summary>
    /// Gets the default settings path relative to the application base directory.
    /// </summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    /// <summary>
    /// Reads the current <c>GroundControl</c> section from the settings file.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The parsed <c>GroundControl</c> section as a <see cref="JsonObject"/>, or <see langword="null"/> if the file or section does not exist.</returns>
    public async Task<JsonObject?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
        var root = JsonNode.Parse(json)?.AsObject();

        return root?["GroundControl"]?.AsObject();
    }

    /// <summary>
    /// Writes the <c>GroundControl</c> section to the settings file, preserving other sections.
    /// </summary>
    /// <param name="groundControlSection">The <c>GroundControl</c> section content to write.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task WriteAsync(JsonObject groundControlSection, CancellationToken cancellationToken = default)
    {
        JsonObject root;

        if (File.Exists(_settingsPath))
        {
            var existing = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
            root = JsonNode.Parse(existing)?.AsObject() ?? [];
        }
        else
        {
            root = [];
        }

        root["GroundControl"] = groundControlSection.DeepClone();

        var output = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(_settingsPath, output, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses a JSON string and extracts the <c>GroundControl</c> section, auto-detecting whether
    /// the root key is present or the input is the inner object.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="section">The extracted <c>GroundControl</c> section.</param>
    /// <param name="error">An error message if parsing fails.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseConfig(string json, out JsonObject? section, out string? error)
    {
        section = null;
        error = null;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }

        if (node is not JsonObject obj)
        {
            error = "Expected a JSON object.";
            return false;
        }

        // Auto-detect: if the root has a "GroundControl" key, unwrap it
        if (obj["GroundControl"] is JsonObject inner)
        {
            obj = inner;
        }

        var serverUrl = obj["ServerUrl"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            error = "Missing required property 'ServerUrl'.";
            return false;
        }

        section = obj.DeepClone().AsObject();
        return true;
    }

    /// <summary>
    /// Builds a list of display-friendly key-value pairs from a <c>GroundControl</c> config section,
    /// masking sensitive values like auth tokens.
    /// </summary>
    /// <param name="section">The <c>GroundControl</c> configuration section.</param>
    /// <returns>A list of key-value pairs suitable for rendering.</returns>
    public static List<(string Key, string Value)> BuildDisplayPairs(JsonObject section)
    {
        List<(string Key, string Value)> pairs = [("ServerUrl", section["ServerUrl"]?.GetValue<string>() ?? "(not set)")];

        if (section["Auth"] is JsonObject auth)
        {
            var method = auth["Method"]?.GetValue<string>();
            if (method is not null)
            {
                pairs.Add(("Auth.Method", method));
            }

            switch (method)
            {
                case "ApiKey":
                    pairs.Add(("Auth.ClientId", auth["ClientId"]?.GetValue<string>() ?? "(not set)"));
                    pairs.Add(("Auth.ClientSecret", MaskValue(auth["ClientSecret"]?.GetValue<string>())));
                    break;

                case "Credentials":
                    pairs.Add(("Auth.Username", auth["Username"]?.GetValue<string>() ?? "(not set)"));
                    pairs.Add(("Auth.Password", MaskValue(auth["Password"]?.GetValue<string>())));
                    break;

                default:
                    pairs.Add(("Auth.Token", MaskValue(auth["Token"]?.GetValue<string>())));
                    break;
            }
        }
        else
        {
            pairs.Add(("Auth", "(none)"));
        }

        return pairs;
    }

    /// <summary>
    /// Masks a sensitive string value, showing only the last 4 characters.
    /// </summary>
    /// <param name="value">The value to mask.</param>
    /// <returns>The masked value.</returns>
    public static string MaskValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(not set)";
        }

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - 4) + value[^4..];
    }
}