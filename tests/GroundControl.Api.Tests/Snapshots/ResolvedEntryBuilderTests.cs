using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Persistence.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class ResolvedEntryBuilderTests
{
    private readonly ResolvedEntryBuilder _sut;

    public ResolvedEntryBuilderTests()
    {
        var scopeResolver = new ScopeResolver(NullLogger<ScopeResolver>.Instance);
        var interpolator = new VariableInterpolator(scopeResolver);
        _sut = new ResolvedEntryBuilder(interpolator);
    }

    [Fact]
    public void Build_SingleVariableWithScopedValues_FansOutEmissionsPerScope()
    {
        // Arrange — scopeless entry referencing a variable that has per-environment values.
        var variable = CreateVariable(
            new ScopedValue("default value", []),
            new ScopedValue("dev value", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod value", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["myVar"] = variable };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{myVar}}", [])];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert — three emissions: dev, prod, unscoped default.
        result.Values.Count.ShouldBe(3);
        result.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "default value");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "dev value");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "prod value");
        result.UnresolvedPlaceholders.ShouldBeEmpty();
        result.UsedSensitiveVariable.ShouldBeFalse();
    }

    [Fact]
    public void Build_TwoVariablesSharedDimension_CombinesByDimensionValueWithoutCartesianMismatch()
    {
        // Arrange — both variables on Environment. The expected behavior is one emission per
        // distinct env value with each variable resolved correctly for that env.
        var first = CreateVariable(
            new ScopedValue("first-default", []),
            new ScopedValue("first-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("first-prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var second = CreateVariable(
            new ScopedValue("second-default", []),
            new ScopedValue("second-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("second-prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable>
        {
            ["first"] = first,
            ["second"] = second,
        };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{first}}/{{second}}", [])];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert — three emissions, each with both vars resolved against the same env value.
        result.Values.Count.ShouldBe(3);
        result.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "first-default/second-default");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "first-dev/second-dev");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "first-prod/second-prod");
    }

    [Fact]
    public void Build_TwoVariablesDisjointDimensions_FansOutAcrossCartesianOfDimensions()
    {
        // Arrange — env-scoped and region-scoped variables with defaults. Cartesian = 4 cells:
        // (dev,us), (dev,unspecified), (unspecified,us), (unspecified,unspecified).
        var envVar = CreateVariable(
            new ScopedValue("env-default", []),
            new ScopedValue("env-dev", new Dictionary<string, string> { ["Environment"] = "dev" }));

        var regionVar = CreateVariable(
            new ScopedValue("region-default", []),
            new ScopedValue("region-us", new Dictionary<string, string> { ["Region"] = "us" }));

        var projectVariables = new Dictionary<string, PlaintextVariable>
        {
            ["env"] = envVar,
            ["region"] = regionVar,
        };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{env}}-{{region}}", [])];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert
        result.Values.Count.ShouldBe(4);
        result.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "env-default-region-default");
        result.Values.ShouldContain(v => v.Scopes.Count == 1 && v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "env-dev-region-default");
        result.Values.ShouldContain(v => v.Scopes.Count == 1 && v.Scopes.GetValueOrDefault("Region") == "us" && v.Value == "env-default-region-us");
        result.Values.ShouldContain(v => v.Scopes.Count == 2 && v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Scopes.GetValueOrDefault("Region") == "us" && v.Value == "env-dev-region-us");
    }

    [Fact]
    public void Build_DisjointDimensions_SourcePinnedToOne_CollapsesPinnedAxisAndFansOutOther()
    {
        // Arrange — the entry source is pinned to Region=us. Variables touch Environment and
        // Region disjointly. The Region axis must collapse to a single value (us) while the
        // Environment axis still fans out. Proves TryMerge and TargetTupleBuilder cooperate when
        // the source scope intersects only one of the variables' dimensions.
        var envVar = CreateVariable(
            new ScopedValue("env-default", []),
            new ScopedValue("env-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("env-prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var regionVar = CreateVariable(
            new ScopedValue("region-default", []),
            new ScopedValue("region-us", new Dictionary<string, string> { ["Region"] = "us" }),
            new ScopedValue("region-eu", new Dictionary<string, string> { ["Region"] = "eu" }));

        var projectVariables = new Dictionary<string, PlaintextVariable>
        {
            ["env"] = envVar,
            ["region"] = regionVar,
        };
        IReadOnlyList<ScopedValue> source =
        [
            new ScopedValue("{{env}}-{{region}}", new Dictionary<string, string> { ["Region"] = "us" }),
        ];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert — only Region=us survives the merge; Environment fans out to {dev, prod, default}.
        result.Values.Count.ShouldBe(3);
        foreach (var emission in result.Values)
        {
            emission.Scopes.GetValueOrDefault("Region").ShouldBe("us");
        }

        result.Values.ShouldContain(v => !v.Scopes.ContainsKey("Environment") && v.Value == "env-default-region-us");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "env-dev-region-us");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "env-prod-region-us");
    }

    [Fact]
    public void Build_SourceScopeConflictsWithVariableScope_DropsConflictingCombinations()
    {
        // Arrange — entry pinned to Env=dev, variable defines Env=dev/Env=prod tuples.
        var variable = CreateVariable(
            new ScopedValue("dev value", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod value", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["myVar"] = variable };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{myVar}}", new Dictionary<string, string> { ["Environment"] = "dev" })];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert — Env=prod target is dropped (conflicts with source); only Env=dev survives.
        var only = result.Values.ShouldHaveSingleItem();
        only.Scopes.GetValueOrDefault("Environment").ShouldBe("dev");
        only.Value.ShouldBe("dev value");
    }

    [Fact]
    public void Build_ExplicitScopedValueWinsOverFanOutWithSameFinalTuple()
    {
        // Arrange — entry has both a default-scope source (referencing a variable) and an explicit
        // Env=prod source with a literal value. The explicit emission must win over the fan-out
        // emission for Env=prod.
        var variable = CreateVariable(
            new ScopedValue("default", []),
            new ScopedValue("var-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("var-prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["myVar"] = variable };
        IReadOnlyList<ScopedValue> source =
        [
            new ScopedValue("{{myVar}}", []),
            new ScopedValue("explicit-prod", new Dictionary<string, string> { ["Environment"] = "prod" }),
        ];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert
        result.Values.Count.ShouldBe(3);
        result.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "default");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "var-dev");

        // The Env=prod emission must be the explicit literal, not the variable's prod tuple.
        var prodEmission = result.Values.Single(v => v.Scopes.GetValueOrDefault("Environment") == "prod");
        prodEmission.Value.ShouldBe("explicit-prod");
    }

    [Fact]
    public void Build_UnknownVariableName_ReportsNameAsUnresolved()
    {
        // Arrange
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{unknown}}", [])];

        // Act
        var result = _sut.Build(source, EmptyLookup, EmptyLookup);

        // Assert
        result.UnresolvedPlaceholders.ShouldContain("unknown");
    }

    [Fact]
    public void Build_VariableWithoutDefault_ReferencedFromUnscopedEntry_ReportsUnresolved()
    {
        // Arrange — variable defines only Env=dev/prod tuples, no default. The unspecified target
        // requires the unscoped default which doesn't exist; strict policy must flag it.
        var variable = CreateVariable(
            new ScopedValue("dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["myVar"] = variable };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{myVar}}", [])];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert
        result.UnresolvedPlaceholders.ShouldContain("myVar");
    }

    [Fact]
    public void Build_SensitiveVariableContributesToOneEmission_SetsEntryUsedSensitiveTrue()
    {
        // Arrange
        var variable = new PlaintextVariable
        {
            Values =
            [
                new ScopedValue("default", []),
                new ScopedValue("dev secret", new Dictionary<string, string> { ["Environment"] = "dev" }),
            ],
            IsSensitive = true,
        };
        var projectVariables = new Dictionary<string, PlaintextVariable> { ["secret"] = variable };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{secret}}", [])];

        // Act
        var result = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert
        result.UsedSensitiveVariable.ShouldBeTrue();
    }

    [Fact]
    public void Build_RepeatedInvocationOnSameInputs_ProducesIdenticalEmissions()
    {
        // Arrange — determinism property: same inputs always yield the same outputs in the same order.
        var variable = CreateVariable(
            new ScopedValue("default", []),
            new ScopedValue("dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var projectVariables = new Dictionary<string, PlaintextVariable> { ["myVar"] = variable };
        IReadOnlyList<ScopedValue> source = [new ScopedValue("{{myVar}}", [])];

        // Act
        var first = _sut.Build(source, projectVariables, EmptyLookup);
        var second = _sut.Build(source, projectVariables, EmptyLookup);

        // Assert — emissions match by position (canonical ordering).
        first.Values.Count.ShouldBe(second.Values.Count);
        for (var i = 0; i < first.Values.Count; i++)
        {
            second.Values[i].Value.ShouldBe(first.Values[i].Value);
            second.Values[i].Scopes.Count.ShouldBe(first.Values[i].Scopes.Count);
            foreach (var pair in first.Values[i].Scopes)
            {
                second.Values[i].Scopes[pair.Key].ShouldBe(pair.Value);
            }
        }
    }

    [Fact]
    public void Build_LiteralValueWithoutPlaceholders_PassesThroughWithSourceScope()
    {
        // Arrange — pure literal entry, no variables referenced.
        IReadOnlyList<ScopedValue> source =
        [
            new ScopedValue("hello", []),
            new ScopedValue("hello-prod", new Dictionary<string, string> { ["Environment"] = "prod" }),
        ];

        // Act
        var result = _sut.Build(source, EmptyLookup, EmptyLookup);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "hello");
        result.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "hello-prod");
    }

    private static PlaintextVariable CreateVariable(params ScopedValue[] values) => new()
    {
        Values = values,
        IsSensitive = false,
    };

    private static readonly Dictionary<string, PlaintextVariable> EmptyLookup = new(StringComparer.OrdinalIgnoreCase);
}