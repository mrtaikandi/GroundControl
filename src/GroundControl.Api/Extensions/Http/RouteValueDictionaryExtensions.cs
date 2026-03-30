using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Api.Extensions.Http;

/// <summary>
/// Extension methods for <see cref="RouteValueDictionary"/>.
/// </summary>
internal static class RouteValueDictionaryExtensions
{
    /// <summary>
    /// Tries to get a route value and convert it to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type to convert the route value to.</typeparam>
    /// <param name="routeValues">The route value dictionary.</param>
    /// <param name="key">The route parameter key.</param>
    /// <param name="value">When this method returns, contains the converted value if successful; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if the route value was found and successfully converted; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetValue<T>(this RouteValueDictionary routeValues, string key, [NotNullWhen(true)] out T? value)
    {
        if (routeValues.TryGetValue(key, out var rawValue) && rawValue is not null)
        {
            if (rawValue is T typed)
            {
                value = typed;
                return true;
            }

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.CanConvertFrom(rawValue.GetType()))
            {
                try
                {
                    value = (T?)converter.ConvertFrom(rawValue);
                    return value is not null;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or NotSupportedException)
                {
                    // Conversion failed — fall through to return false
                }
            }
        }

        value = default;
        return false;
    }
}