---
name: xunit-testing
description: Writes and runs unit tests using xUnit v3 with Microsoft Testing Platform. Apply when creating test classes, testing with mocks, or running tests in dotnet projects. Uses Shouldly for assertions and NSubstitute for mocking.
---

# xUnit v3 Testing

This project uses **xUnit v3** with **Microsoft Testing Platform** (MTP), **Shouldly** for assertions, and **NSubstitute** for mocking.

## Test Organization

- **Project**: `{ProjectName}.Tests` (e.g., `ProjectName.Api.Tests`)
- **Namespace**: Mirror source namespace (e.g., `ProjectName.Api.Features` -> `ProjectName.Api.Tests.Features`)
- **File naming**: `{ClassUnderTest}Tests.cs`
- **Method naming**: `{MethodUnderTest}_{Scenario}_{ExpectedBehavior}`
- **Structure**: Use `// Arrange`, `// Act`, `// Assert` comments

## Test Project Setup

Required `.csproj` configuration:

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="xunit.v3" />
  <PackageReference Include="xunit.runner.visualstudio">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Shouldly" />
  <PackageReference Include="NSubstitute" />
</ItemGroup>
```

Implicit usings for test projects are configured in `Properties/Usings.cs` file in the test project. For example: `ProjectName.Api.Tests/Properties/Usings.cs`

## Comment on Tests
DO NOT write comments in test methods except for the `// Arrange`, `// Act`, `// Assert` pattern.

## Example: Test with Mocked Dependencies

This is the typical test pattern for this project:

```csharp
using NSubstitute;
using Shouldly;

namespace ProjectName.Api.Tests.Features.Example;

public class OrderServiceTests
{
    [Fact]
    public async Task ProcessOrder_WithValidOrder_ReturnsSuccess()
    {
        // Arrange
        var repository = Substitute.For<IOrderRepository>();
        repository.SaveAsync(Arg.Any<Order>()).Returns(true);

        var service = new OrderService(repository);
        var order = new Order { Id = 1 };

        // Act
        var result = await service.ProcessOrderAsync(order);

        // Assert
        result.ShouldBeTrue();
        await repository.Received(1).SaveAsync(order);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessInput_WithInvalidInput_ThrowsArgumentException(string? input)
    {
        // Arrange
        var service = new ValidationService();

        // Act & Assert
        Should.Throw<ArgumentException>(() => service.ProcessInput(input!));
    }
}
```

## Running Tests

```bash
# Run all tests
dotnet test ProjectName.Api.Tests/ProjectName.Api.Tests.csproj

# Run specific test by name
dotnet test ProjectName.Api.Tests/ProjectName.Api.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run specific test class
dotnet test --filter-class OrderServiceTests
```

## Reference

For Shouldly assertion cheatsheet, xUnit patterns (fixtures, MemberData, lifecycle), and debugging tips, see [testing-reference.md](testing-reference.md).