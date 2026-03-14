using GroundControl.Api.Shared.Resolvers;
using GroundControl.Persistence.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Resolvers;

public sealed class ScopeResolverTests
{
    private readonly ScopeResolver _sut = new(NullLogger<ScopeResolver>.Instance);

    [Fact]
    public void Resolve_ExactScopeMatch_ReturnsMatchingScopedValue()
    {
        // Arrange
        var expected = new ScopedValue("prod-eu", new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU"
        });

        var scopedValues = new List<ScopedValue>
        {
            new("default", []),
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" }),
            expected
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_PartialScopeMatch_ReturnsMostSpecificMatch()
    {
        // Arrange
        var lessSpecific = new ScopedValue("prod", new Dictionary<string, string>
        {
            ["environment"] = "Production"
        });

        var moreSpecific = new ScopedValue("prod-eu", new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU"
        });

        var scopedValues = new List<ScopedValue>
        {
            new("default", []),
            lessSpecific,
            moreSpecific
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU",
            ["tier"] = "Premium"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(moreSpecific);
    }

    [Fact]
    public void Resolve_NoScopeMatch_WithUnscopedDefault_ReturnsDefault()
    {
        // Arrange
        var unscopedDefault = new ScopedValue("default-value", []);

        var scopedValues = new List<ScopedValue>
        {
            unscopedDefault,
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" })
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Staging"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(unscopedDefault);
    }

    [Fact]
    public void Resolve_NoScopeMatch_NoDefault_ReturnsNull()
    {
        // Arrange
        var scopedValues = new List<ScopedValue>
        {
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" }),
            new("staging", new Dictionary<string, string> { ["environment"] = "Staging" })
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Development"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_EmptyClientScopes_ReturnsUnscopedDefault()
    {
        // Arrange
        var unscopedDefault = new ScopedValue("default-value", []);

        var scopedValues = new List<ScopedValue>
        {
            unscopedDefault,
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" })
        };

        var clientScopes = new Dictionary<string, string>();

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(unscopedDefault);
    }

    [Fact]
    public void Resolve_EmptyClientScopes_NoDefault_ReturnsNull()
    {
        // Arrange
        var scopedValues = new List<ScopedValue>
        {
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" })
        };

        var clientScopes = new Dictionary<string, string>();

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_EmptyScopedValues_ReturnsNull()
    {
        // Arrange
        var scopedValues = new List<ScopedValue>();
        var clientScopes = new Dictionary<string, string> { ["environment"] = "Production" };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_SpecificityTie_ReturnsFirstAndLogsWarning()
    {
        // Arrange
        using var collectingProvider = new CollectingLoggerProvider();
        using var loggerFactory = new LoggerFactory([collectingProvider]);
        var sut = new ScopeResolver(loggerFactory.CreateLogger<ScopeResolver>());

        var first = new ScopedValue("by-env", new Dictionary<string, string>
        {
            ["environment"] = "Production"
        });

        var second = new ScopedValue("by-region", new Dictionary<string, string>
        {
            ["region"] = "EU"
        });

        var scopedValues = new List<ScopedValue> { first, second };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU"
        };

        // Act
        var result = sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(first);
        collectingProvider.Entries.ShouldContain(e => e.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public void Resolve_CandidateScopeValueMismatch_DoesNotMatch()
    {
        // Arrange
        var unscopedDefault = new ScopedValue("default", []);

        var scopedValues = new List<ScopedValue>
        {
            unscopedDefault,
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" })
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Staging"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(unscopedDefault);
    }

    [Fact]
    public void Resolve_CandidateHasDimensionNotInClientScopes_DoesNotMatch()
    {
        // Arrange
        var scopedValues = new List<ScopedValue>
        {
            new("prod-eu", new Dictionary<string, string>
            {
                ["environment"] = "Production",
                ["region"] = "EU"
            })
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_ClientHasExtraDimensions_StillMatchesSubset()
    {
        // Arrange
        var expected = new ScopedValue("prod", new Dictionary<string, string>
        {
            ["environment"] = "Production"
        });

        var scopedValues = new List<ScopedValue> { expected };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU",
            ["tier"] = "Premium"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_OnlyUnscopedDefault_ReturnsIt()
    {
        // Arrange
        var unscopedDefault = new ScopedValue("default-value", []);
        var scopedValues = new List<ScopedValue> { unscopedDefault };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(unscopedDefault);
    }

    [Fact]
    public void Resolve_ScopeMatchExists_UnscopedDefaultIgnored()
    {
        // Arrange
        var unscopedDefault = new ScopedValue("default", []);
        var scopedMatch = new ScopedValue("prod", new Dictionary<string, string>
        {
            ["environment"] = "Production"
        });

        var scopedValues = new List<ScopedValue> { unscopedDefault, scopedMatch };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBe(scopedMatch);
    }

    [Fact]
    public void Resolve_CaseSensitiveScopeValues_DoesNotMatchDifferentCase()
    {
        // Arrange
        var scopedValues = new List<ScopedValue>
        {
            new("prod", new Dictionary<string, string> { ["environment"] = "Production" })
        };

        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "production"
        };

        // Act
        var result = _sut.Resolve(scopedValues, clientScopes);

        // Assert
        result.ShouldBeNull();
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(this);

        public void Dispose() { }
    }

    private sealed class CollectingLogger(CollectingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            provider.Entries.Add(new LogEntry { LogLevel = logLevel, Message = formatter(state, exception) });
        }
    }

    internal sealed class LogEntry
    {
        public LogLevel LogLevel { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}