namespace GroundControl.Host.Api.Generators.Tests;

public sealed class ValidatorRegistrationTests
{
    [Fact]
    public Task OptionsModule_WithNestedValidator()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;
            using Microsoft.Extensions.Options;

            public class MyOptions
            {
                public string Value { get; set; } = "";

                public sealed class Validator : IValidateOptions<MyOptions>
                {
                    public ValidateOptionsResult Validate(string? name, MyOptions options)
                    {
                        return ValidateOptionsResult.Success;
                    }
                }
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task OptionsModule_WithRootValidator()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;
            using Microsoft.Extensions.Options;

            public class MyOptions
            {
                public string Value { get; set; } = "";
            }

            public sealed class MyOptionsValidator : IValidateOptions<MyOptions>
            {
                public ValidateOptionsResult Validate(string? name, MyOptions options)
                {
                    return ValidateOptionsResult.Success;
                }
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task OptionsModule_WithoutValidator()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;

            public class MyOptions
            {
                public string Value { get; set; } = "";
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task OptionsModule_MultipleValidators()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;
            using Microsoft.Extensions.Options;

            public class MyOptions
            {
                public string Value { get; set; } = "";

                public sealed class FirstValidator : IValidateOptions<MyOptions>
                {
                    public ValidateOptionsResult Validate(string? name, MyOptions options)
                    {
                        return ValidateOptionsResult.Success;
                    }
                }

                public sealed class SecondValidator : IValidateOptions<MyOptions>
                {
                    public ValidateOptionsResult Validate(string? name, MyOptions options)
                    {
                        return ValidateOptionsResult.Success;
                    }
                }
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task OptionsModule_AbstractValidator_Ignored()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;
            using Microsoft.Extensions.Options;

            public class MyOptions
            {
                public string Value { get; set; } = "";

                public abstract class Validator : IValidateOptions<MyOptions>
                {
                    public abstract ValidateOptionsResult Validate(string? name, MyOptions options);
                }
            }

            internal sealed class MyModule : IWebApiModule<MyOptions>
            {
                public MyModule(MyOptions options) { }
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }

    [Fact]
    public Task PlainModule_NoValidatorRegistration()
    {
        // Arrange
        var source = """
            using GroundControl.Host.Api;
            using Microsoft.Extensions.Options;

            public class SomeOptions
            {
                public sealed class Validator : IValidateOptions<SomeOptions>
                {
                    public ValidateOptionsResult Validate(string? name, SomeOptions options)
                    {
                        return ValidateOptionsResult.Success;
                    }
                }
            }

            internal sealed class MyModule : IWebApiModule
            {
                public void OnServiceConfiguration(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder) { }
                public void OnApplicationConfiguration(Microsoft.AspNetCore.Builder.WebApplication app) { }
            }
            """;

        // Act
        var driver = GeneratorTestHelper.CreateDriver(GeneratorTestHelper.CreateCompilation(source));

        // Assert
        return Verify(driver)
            .IgnoreGeneratedResult(r => r.HintName.StartsWith("GroundControl.Host.Api.", StringComparison.Ordinal));
    }
}