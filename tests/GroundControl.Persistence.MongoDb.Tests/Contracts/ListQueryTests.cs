using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Contracts;

public sealed class ListQueryTests
{
    [Fact]
    public void Validate_WithAfterAndBefore_ThrowsValidationException()
    {
        // Arrange
        var query = new ListQuery
        {
            After = "after-cursor",
            Before = "before-cursor"
        };

        // Act & Assert
        Should.Throw<ValidationException>(() => Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true));
    }

    [Fact]
    public void Validate_WithLimitAboveMaximum_ThrowsValidationException()
    {
        // Arrange
        var query = new ListQuery
        {
            Limit = 101
        };

        // Act & Assert
        Should.Throw<ValidationException>(() => Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true));
    }

    [Fact]
    public void Validate_WithValidPagingSettings_DoesNotThrow()
    {
        // Arrange
        var query = new ListQuery
        {
            Limit = 100,
            After = "after-cursor",
            SortField = "name",
            SortOrder = "asc"
        };

        // Act
        var exception = Record.Exception(() => Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true));

        // Assert
        exception.ShouldBeNull();
    }
}