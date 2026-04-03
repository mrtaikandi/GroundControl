using System.Globalization;
using System.Text.Json.Nodes;
using GroundControl.Host.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace GroundControl.Api.Core.OpenApi;

internal sealed class OpenApiModule : IWebApiModule
{
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

    public void OnServiceConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer(AddJsonEnumSchemaTransformer);
            options.AddDocumentTransformer(AddProblemDetailsDocumentTransformer);
        });
    }

    private static Task AddJsonEnumSchemaTransformer(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (!type.IsEnum)
        {
            return Task.CompletedTask;
        }

        var values = Enum.GetValuesAsUnderlyingType(type);
        var enumValues = new List<JsonNode>();
        foreach (var value in values)
        {
            enumValues.Add(JsonValue.Create(Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        }

        schema.Enum = enumValues;
        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-enumNames"] = new JsonNodeExtension(new JsonArray(Enum.GetNames(type).Select(JsonNode (name) => JsonValue.Create(name)).ToArray()));

        return Task.CompletedTask;
    }

    private static Task AddProblemDetailsDocumentTransformer(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
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
                new OpenApiSchemaReference(nameof(ProblemDetails), document), new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = errorsSchema is null
                        ? []
                        : new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["errors"] = errorsSchema }
                }
            ]
        };

        return Task.CompletedTask;
    }
}