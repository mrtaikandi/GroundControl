using GroundControl.Host.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace GroundControl.Api.Core.OpenApi;

internal sealed class OpenApiModule : IWebApiModule
{
    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                var schemas = document.Components?.Schemas;
                if (schemas is null || !schemas.TryGetValue(nameof(HttpValidationProblemDetails), out var validationProblemDetailsSchema))
                {
                    return Task.CompletedTask;
                }

                IOpenApiSchema? errorsSchema = null;
                var properties = validationProblemDetailsSchema.Properties;
                properties?.TryGetValue("errors", out errorsSchema);

                schemas[nameof(HttpValidationProblemDetails)] = new OpenApiSchema
                {
                    Description = validationProblemDetailsSchema.Description,
                    AllOf =
                    [
                        new OpenApiSchemaReference(nameof(ProblemDetails), document),
                        new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = errorsSchema is null
                                ? []
                                : new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                                {
                                    ["errors"] = errorsSchema
                                }
                        }
                    ]
                };

                return Task.CompletedTask;
            });
        });
    }

    public void OnApplicationConfiguration(WebApplication app)
    {
        app.MapOpenApi();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference(options =>
            {
                options.DarkMode = true;
                options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }
    }
}