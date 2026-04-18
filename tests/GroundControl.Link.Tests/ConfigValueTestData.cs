namespace GroundControl.Link.Tests;

internal static class ConfigValueTestData
{
    public static ConfigValue V(string value, bool isSensitive = false) => new() { Value = value, IsSensitive = isSensitive };

    public static Dictionary<string, ConfigValue> Dict(params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            dict[key] = new ConfigValue { Value = value };
        }

        return dict;
    }
}