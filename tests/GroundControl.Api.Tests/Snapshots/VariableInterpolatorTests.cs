using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Persistence.Contracts;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class VariableInterpolatorTests
{
    private readonly IScopeResolver _scopeResolver = Substitute.For<IScopeResolver>();
    private readonly VariableInterpolator _sut;

    public VariableInterpolatorTests()
    {
        _sut = new VariableInterpolator(_scopeResolver);
    }

    [Fact]
    public void Interpolate_SinglePlaceholderResolved_ReturnsSubstitutedValue()
    {
        // Arrange
        var variable = CreateVariable("Production");
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["envName"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string> { ["env"] = "prod" };

        SetupResolve(variable, clientScopes, "Production");

        // Act
        var result = _sut.Interpolate("Server={{envName}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Server=Production");
        result.UnresolvedPlaceholders.ShouldBeEmpty();
        result.IsFullyResolved.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_ProjectVariableOverridesGlobal_UsesProjectVariableValue()
    {
        // Arrange
        var projectVariable = CreateVariable("project-db.local");
        var globalVariable = CreateVariable("global-db.local");

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["dbHost"] = projectVariable };
        var globalVariables = new Dictionary<string, PlaintextVariable> { ["dbHost"] = globalVariable };
        var clientScopes = new Dictionary<string, string> { ["env"] = "prod" };

        SetupResolve(projectVariable, clientScopes, "project-db.local");

        // Act
        var result = _sut.Interpolate("Host={{dbHost}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Host=project-db.local");
        result.IsFullyResolved.ShouldBeTrue();

        // Global variable should never be consulted
        _scopeResolver.DidNotReceive().Resolve(
            Arg.Is<IReadOnlyList<ScopedValue>>(v => v == globalVariable.Values || v.SequenceEqual(globalVariable.Values)),
            Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public void Interpolate_FallsBackToGlobalVariable_WhenProjectVariableNotFound()
    {
        // Arrange
        var globalVariable = CreateVariable("global-key-123");
        Dictionary<string, PlaintextVariable> projectVariables = [];
        var globalVariables = new Dictionary<string, PlaintextVariable> { ["apiKey"] = globalVariable };
        var clientScopes = new Dictionary<string, string> { ["env"] = "prod" };

        SetupResolve(globalVariable, clientScopes, "global-key-123");

        // Act
        var result = _sut.Interpolate("Key={{apiKey}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Key=global-key-123");
        result.IsFullyResolved.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_UnresolvedPlaceholder_ReturnsNameInUnresolvedList()
    {
        // Arrange
        Dictionary<string, PlaintextVariable> projectVariables = [];
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        // Act
        var result = _sut.Interpolate("Value={{missing}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Value={{missing}}");
        result.UnresolvedPlaceholders.ShouldContain("missing");
        result.IsFullyResolved.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_MultiplePlaceholders_AllSubstituted()
    {
        // Arrange
        var hostVar = CreateVariable("db.local");
        var portVar = CreateVariable("5432");

        var projectVariables = new Dictionary<string, PlaintextVariable>
        {
            ["host"] = hostVar,
            ["port"] = portVar
        };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string> { ["env"] = "prod" };

        SetupResolve(hostVar, clientScopes, "db.local");
        SetupResolve(portVar, clientScopes, "5432");

        // Act
        var result = _sut.Interpolate("Server={{host}}:{{port}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Server=db.local:5432");
        result.UnresolvedPlaceholders.ShouldBeEmpty();
        result.IsFullyResolved.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_NoPlaceholders_ReturnsOriginalStringUnchanged()
    {
        // Arrange
        Dictionary<string, PlaintextVariable> projectVariables = [];
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        // Act
        var result = _sut.Interpolate("plain value with no placeholders", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("plain value with no placeholders");
        result.UnresolvedPlaceholders.ShouldBeEmpty();
        result.IsFullyResolved.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_ScopeAwareResolution_CallsScopeResolverWithClientScopes()
    {
        // Arrange
        var variable = CreateVariable("eu-west-1");
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["region"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>
        {
            ["environment"] = "Production",
            ["region"] = "EU"
        };

        SetupResolve(variable, clientScopes, "eu-west-1");

        // Act
        _sut.Interpolate("Region={{region}}", clientScopes, projectVariables, globalVariables);

        // Assert
        _scopeResolver.Received(1).Resolve(
            Arg.Any<IReadOnlyList<ScopedValue>>(),
            clientScopes);
    }

    [Fact]
    public void Interpolate_MixedResolvedAndUnresolved_ReturnsPartialResult()
    {
        // Arrange
        var knownVar = CreateVariable("resolved-value");
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["known"] = knownVar };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(knownVar, clientScopes, "resolved-value");

        // Act
        var result = _sut.Interpolate("{{known}}-{{unknown}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("resolved-value-{{unknown}}");
        result.UnresolvedPlaceholders.ShouldBe(["unknown"]);
        result.IsFullyResolved.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_VariableExistsButScopeResolverReturnsNull_TreatedAsUnresolved()
    {
        // Arrange
        var variable = CreateVariable("Production");
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["envName"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string> { ["env"] = "staging" };

        // ScopeResolver returns null (no matching scope)
        _scopeResolver.Resolve(Arg.Any<IReadOnlyList<ScopedValue>>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns((ScopedValue?)null);

        // Act
        var result = _sut.Interpolate("Env={{envName}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Env={{envName}}");
        result.UnresolvedPlaceholders.ShouldContain("envName");
        result.IsFullyResolved.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_DuplicatePlaceholder_ResolvedOnce()
    {
        // Arrange
        var variable = CreateVariable("prod");
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["env"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(variable, clientScopes, "prod");

        // Act
        var result = _sut.Interpolate("{{env}}-{{env}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("prod-prod");
        result.UnresolvedPlaceholders.ShouldBeEmpty();
    }

    [Fact]
    public void Interpolate_NoPlaceholders_LeavesUsedSensitiveVariableFalse()
    {
        // Arrange
        Dictionary<string, PlaintextVariable> projectVariables = [];
        Dictionary<string, PlaintextVariable> globalVariables = [];

        // Act
        var result = _sut.Interpolate("plain literal", new Dictionary<string, string>(), projectVariables, globalVariables);

        // Assert
        result.UsedSensitiveVariable.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_ResolvedSensitiveVariable_SetsUsedSensitiveVariableTrue()
    {
        // Arrange
        var variable = CreateVariable("hunter2", isSensitive: true);
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["dbPassword"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(variable, clientScopes, "hunter2");

        // Act
        var result = _sut.Interpolate("Password={{dbPassword}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Password=hunter2");
        result.UsedSensitiveVariable.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_ResolvedNonSensitiveVariable_LeavesUsedSensitiveVariableFalse()
    {
        // Arrange
        var variable = CreateVariable("db.local", isSensitive: false);
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["dbHost"] = variable };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(variable, clientScopes, "db.local");

        // Act
        var result = _sut.Interpolate("Host={{dbHost}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.UsedSensitiveVariable.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_MultiplePlaceholdersOneSensitive_SetsUsedSensitiveVariableTrue()
    {
        // Arrange
        var hostVar = CreateVariable("db.local", isSensitive: false);
        var passwordVar = CreateVariable("hunter2", isSensitive: true);

        var projectVariables = new Dictionary<string, PlaintextVariable>
        {
            ["host"] = hostVar,
            ["dbPassword"] = passwordVar,
        };
        Dictionary<string, PlaintextVariable> globalVariables = [];
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(hostVar, clientScopes, "db.local");
        SetupResolve(passwordVar, clientScopes, "hunter2");

        // Act
        var result = _sut.Interpolate("Server={{host}};Password={{dbPassword}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("Server=db.local;Password=hunter2");
        result.UsedSensitiveVariable.ShouldBeTrue();
    }

    [Fact]
    public void Interpolate_UnresolvedPlaceholderOnly_LeavesUsedSensitiveVariableFalse()
    {
        // Arrange
        Dictionary<string, PlaintextVariable> projectVariables = [];
        Dictionary<string, PlaintextVariable> globalVariables = [];

        // Act
        var result = _sut.Interpolate("{{missing}}", new Dictionary<string, string>(), projectVariables, globalVariables);

        // Assert
        result.UsedSensitiveVariable.ShouldBeFalse();
    }

    [Fact]
    public void Interpolate_SensitiveProjectVariableOverridesNonSensitiveGlobal_SetsUsedSensitiveVariableTrue()
    {
        // Arrange
        var projectVar = CreateVariable("project-secret", isSensitive: true);
        var globalVar = CreateVariable("global-public", isSensitive: false);

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["token"] = projectVar };
        var globalVariables = new Dictionary<string, PlaintextVariable> { ["token"] = globalVar };
        var clientScopes = new Dictionary<string, string>();

        SetupResolve(projectVar, clientScopes, "project-secret");

        // Act
        var result = _sut.Interpolate("T={{token}}", clientScopes, projectVariables, globalVariables);

        // Assert
        result.Value.ShouldBe("T=project-secret");
        result.UsedSensitiveVariable.ShouldBeTrue();
    }

    private static PlaintextVariable CreateVariable(string defaultValue, bool isSensitive = false) => new()
    {
        Values = [new ScopedValue(defaultValue, [])],
        IsSensitive = isSensitive,
    };

    private void SetupResolve(PlaintextVariable variable, IReadOnlyDictionary<string, string> clientScopes, string resolvedValue)
    {
        _scopeResolver.Resolve(
                Arg.Is<IReadOnlyList<ScopedValue>>(v => v.SequenceEqual(variable.Values)),
                clientScopes)
            .Returns(new ScopedValue(resolvedValue, []));
    }
}