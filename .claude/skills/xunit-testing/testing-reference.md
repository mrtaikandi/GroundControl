# Testing Reference

## Shouldly Assertions Cheatsheet

```csharp
// Equality
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);

// Nullability
obj.ShouldBeNull();
obj.ShouldNotBeNull();

// Collections
list.ShouldBeEmpty();
list.ShouldNotBeEmpty();
list.ShouldContain(item);

// Booleans
flag.ShouldBeTrue();
flag.ShouldBeFalse();

// Strings
str.ShouldStartWith("prefix");
str.ShouldEndWith("suffix");
str.ShouldContain("substring");

// Exceptions
Should.Throw<ArgumentException>(() => method());
Should.NotThrow(() => method());
```

## Test Lifecycle

```csharp
// Constructor = setup (runs before each test)
// IDisposable.Dispose = teardown (runs after each test)
public class MyTests : IDisposable
{
    private readonly MyService _service;

    public MyTests() => _service = new MyService();
    public void Dispose() => _service.Dispose();
}
```

## Shared Context (Class Fixture)

Use `IClassFixture<T>` for expensive setup that runs once per test class:

```csharp
public class DatabaseFixture : IDisposable
{
    public TestDatabase Database { get; } = new();
    public void Dispose() => Database.Dispose();
}

public class MyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    public MyTests(DatabaseFixture fixture) => _fixture = fixture;
}
```

## Theory with MemberData

For complex test data beyond what `[InlineData]` supports:

```csharp
public static IEnumerable<object[]> TestData =>
[
    [1, 2, 3],
    [-1, 1, 0],
];

[Theory]
[MemberData(nameof(TestData))]
public void Add_WithVariousInputs_ReturnsExpected(int a, int b, int expected)
{
    Calculator.Add(a, b).ShouldBe(expected);
}
```

## CLI Options

```bash
# Coverage
dotnet test --collect:"XPlat Code Coverage"

# Diagnostics
dotnet test --diagnostic

# Parallelism
dotnet test --max-threads 4

# Stop on first failure
dotnet test --stop-on-fail on

# CI/CD with TRX output
dotnet test --logger "trx" --results-directory ./TestResults
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All tests passed |
| 1 | General failure |
| 2 | Invalid arguments |
| 8 | No tests discovered |