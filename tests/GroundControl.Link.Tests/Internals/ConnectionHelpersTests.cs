namespace GroundControl.Link.Tests.Internals;

public sealed class ConnectionHelpersTests
{
    [Fact]
    public void AddJitter_ReturnsValueBetween75And100Percent()
    {
        // Arrange
        var baseDelay = TimeSpan.FromSeconds(10);

        // Act & Assert — run multiple times to exercise the random range
        for (var i = 0; i < 100; i++)
        {
            var result = ConnectionHelpers.AddJitter(baseDelay);
            result.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(7500);
            result.TotalMilliseconds.ShouldBeLessThanOrEqualTo(12500);
        }
    }

    [Fact]
    public void AddJitter_VerySmallDelay_ReturnsAtLeast100Ms()
    {
        // Arrange
        var baseDelay = TimeSpan.FromMilliseconds(10);

        // Act
        var result = ConnectionHelpers.AddJitter(baseDelay);

        // Assert
        result.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(100);
    }

}