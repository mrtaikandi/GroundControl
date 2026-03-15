using GroundControl.Api.Shared.Security.Authorization;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security.Authorization;

public sealed class ScopeValueFilterTests
{
    private static readonly ScopedValue UnscopedValue = new("default", []);
    private static readonly ScopedValue ProductionValue = new("prod", new Dictionary<string, string> { ["environment"] = "Production" });
    private static readonly ScopedValue StagingValue = new("staging", new Dictionary<string, string> { ["environment"] = "Staging" });
    private static readonly ScopedValue ProductionEuValue = new("prod-eu", new Dictionary<string, string> { ["environment"] = "Production", ["region"] = "EU" });

    private static readonly ScopedValue[] AllValues = [UnscopedValue, ProductionValue, StagingValue, ProductionEuValue];

    [Fact]
    public void NullConditions_ReturnsAllValues()
    {
        // Arrange
        Grant[] grants = [new Grant { RoleId = Guid.CreateVersion7(), Conditions = null! }];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void EmptyConditions_ReturnsAllValues()
    {
        // Arrange
        Grant[] grants = [new Grant { RoleId = Guid.CreateVersion7() }];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void SingleCondition_FiltersToMatchingValues()
    {
        // Arrange — only allow Production environment
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Production"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — unscoped (default) + Production + ProductionEU
        result.Count.ShouldBe(3);
        result.ShouldContain(UnscopedValue);
        result.ShouldContain(ProductionValue);
        result.ShouldContain(ProductionEuValue);
    }

    [Fact]
    public void MultipleValuesInCondition_MatchesAny()
    {
        // Arrange — allow Production OR Staging
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Production", "Staging"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — unscoped + Production + Staging + ProductionEU
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void MultipleConditionKeys_AreAndCombined()
    {
        // Arrange — environment=Production AND region=EU
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Production"],
                    ["region"] = ["EU"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — unscoped + ProductionEU (Production has no region dimension, so condition doesn't restrict it)
        // Production value has environment=Production but no region key → region condition doesn't apply → allowed
        result.Count.ShouldBe(3);
        result.ShouldContain(UnscopedValue);
        result.ShouldContain(ProductionValue);
        result.ShouldContain(ProductionEuValue);
    }

    [Fact]
    public void ConditionBlocksStagingWithRegionEuRestriction()
    {
        // Arrange — only allow environment=Staging AND region=EU
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Staging"],
                    ["region"] = ["EU"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — unscoped + Staging (has environment=Staging, no region → allowed)
        // Production has environment=Production → blocked by environment condition
        // ProductionEU has environment=Production → blocked by environment condition
        result.Count.ShouldBe(2);
        result.ShouldContain(UnscopedValue);
        result.ShouldContain(StagingValue);
    }

    [Fact]
    public void UnscopedValues_AlwaysIncluded()
    {
        // Arrange — restrictive condition that matches nothing
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["NonExistent"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — only unscoped
        result.Count.ShouldBe(1);
        result.ShouldContain(UnscopedValue);
    }

    [Fact]
    public void MultipleGrants_UnionConditions()
    {
        // Arrange — one grant allows Production, another allows Staging
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Production"]
                }
            },
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Staging"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — all values allowed (unscoped + Production + Staging + ProductionEU)
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void GrantWithUnrestrictedConditions_TrumpsRestrictiveGrants()
    {
        // Arrange — one restrictive grant and one unrestricted
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["Production"]
                }
            },
            new Grant
            {
                RoleId = Guid.CreateVersion7()
                // Empty conditions = unrestricted
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — all values returned (unrestricted grant trumps)
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void CaseSensitiveMatching()
    {
        // Arrange — condition uses lowercase "production" but data has "Production"
        Grant[] grants =
        [
            new Grant
            {
                RoleId = Guid.CreateVersion7(),
                Conditions = new Dictionary<string, List<string>>
                {
                    ["environment"] = ["production"]
                }
            }
        ];

        // Act
        var result = ScopeValueFilter.Filter(AllValues, grants);

        // Assert — only unscoped (case mismatch blocks all scoped values)
        result.Count.ShouldBe(1);
        result.ShouldContain(UnscopedValue);
    }

    [Fact]
    public void EmptyValues_ReturnsEmpty()
    {
        // Arrange
        Grant[] grants = [new Grant { RoleId = Guid.CreateVersion7() }];

        // Act
        var result = ScopeValueFilter.Filter([], grants);

        // Assert
        result.ShouldBeEmpty();
    }
}