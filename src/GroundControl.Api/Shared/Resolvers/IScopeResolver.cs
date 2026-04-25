using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Resolvers;

/// <summary>
/// Resolves the most specific <see cref="ScopedValue" /> that matches a client's scope dimensions.
/// </summary>
public interface IScopeResolver
{
    /// <summary>
    /// Selects the best-matching <see cref="ScopedValue" /> for the given client scopes.
    /// </summary>
    /// <param name="scopedValues">The candidate scoped values to resolve against.</param>
    /// <param name="clientScopes">The client's scope dimension-value pairs.</param>
    /// <returns>
    /// The most specific matching <see cref="ScopedValue" />, the unscoped default if no scoped match exists,
    /// or <see langword="null" /> if no match is found at all.
    /// </returns>
    ScopedValue? Resolve(IReadOnlyList<ScopedValue> scopedValues, IReadOnlyDictionary<string, string> clientScopes);
}