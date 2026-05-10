using GroundControl.Api.Features.Snapshots;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class TargetTupleBuilderTests
{
    [Fact]
    public void Build_SingleVariableSingleDimension_ProducesTuplePerValuePlusUnspecified()
    {
        // Arrange
        var variable = CreateVariable(
            new ScopedValue("default", []),
            new ScopedValue("dev value", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod value", new Dictionary<string, string> { ["Environment"] = "prod" }));

        // Act
        var targets = TargetTupleBuilder.Build([variable]);

        // Assert
        targets.Count.ShouldBe(3);
        targets.ShouldContain(t => t.Count == 0);
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "dev");
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "prod");
    }

    [Fact]
    public void Build_VariableWithOnlyDefault_ProducesEmptyTupleOnly()
    {
        // Arrange — variable has no scoped tuples, only the unscoped default.
        var variable = CreateVariable(new ScopedValue("default", []));

        // Act
        var targets = TargetTupleBuilder.Build([variable]);

        // Assert
        var only = targets.ShouldHaveSingleItem();
        only.Count.ShouldBe(0);
    }

    [Fact]
    public void Build_TwoVariablesSharedDimension_CollapsesToSingleTupleSet()
    {
        // Arrange — both variables touch Environment with overlapping and unique values.
        var first = CreateVariable(
            new ScopedValue("a-default", []),
            new ScopedValue("a-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("a-prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var second = CreateVariable(
            new ScopedValue("b-default", []),
            new ScopedValue("b-dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("b-staging", new Dictionary<string, string> { ["Environment"] = "staging" }));

        // Act
        var targets = TargetTupleBuilder.Build([first, second]);

        // Assert — Environment domain is {dev, prod, staging, unspecified} -> 4 tuples, no duplicates.
        targets.Count.ShouldBe(4);
        targets.ShouldContain(t => t.Count == 0);
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "dev");
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "prod");
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "staging");
    }

    [Fact]
    public void Build_TwoVariablesDisjointDimensions_ProducesCartesianProduct()
    {
        // Arrange — one variable touches Environment, another touches Region.
        var envVariable = CreateVariable(
            new ScopedValue("dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        var regionVariable = CreateVariable(
            new ScopedValue("us", new Dictionary<string, string> { ["Region"] = "us" }),
            new ScopedValue("eu", new Dictionary<string, string> { ["Region"] = "eu" }));

        // Act
        var targets = TargetTupleBuilder.Build([envVariable, regionVariable]);

        // Assert — (dev, prod, unspecified) x (us, eu, unspecified) = 9 tuples.
        targets.Count.ShouldBe(9);
        targets.ShouldContain(t => t.Count == 0);
        targets.ShouldContain(t => t.Count == 1 && t.GetValueOrDefault("Environment") == "dev");
        targets.ShouldContain(t => t.Count == 1 && t.GetValueOrDefault("Region") == "us");
        targets.ShouldContain(t => t.Count == 2 && t.GetValueOrDefault("Environment") == "dev" && t.GetValueOrDefault("Region") == "us");
        targets.ShouldContain(t => t.Count == 2 && t.GetValueOrDefault("Environment") == "prod" && t.GetValueOrDefault("Region") == "eu");
    }

    [Fact]
    public void Build_VariableWithoutDefault_StillEmitsUnspecifiedTuple()
    {
        // Arrange — variable has scoped tuples but no unscoped default. Strict policy lets the
        // resolver flag the unspecified target as unresolved at interpolation time; the builder's
        // job is just to enumerate the targets.
        var variable = CreateVariable(
            new ScopedValue("dev", new Dictionary<string, string> { ["Environment"] = "dev" }),
            new ScopedValue("prod", new Dictionary<string, string> { ["Environment"] = "prod" }));

        // Act
        var targets = TargetTupleBuilder.Build([variable]);

        // Assert
        targets.Count.ShouldBe(3);
        targets.ShouldContain(t => t.Count == 0);
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "dev");
        targets.ShouldContain(t => t.Count == 1 && t["Environment"] == "prod");
    }

    [Fact]
    public void Build_NoReferencedVariables_ReturnsSingleEmptyTuple()
    {
        // Act
        var targets = TargetTupleBuilder.Build([]);

        // Assert
        var only = targets.ShouldHaveSingleItem();
        only.Count.ShouldBe(0);
    }

    private static PlaintextVariable CreateVariable(params ScopedValue[] values) => new()
    {
        Values = values,
        IsSensitive = false,
    };
}