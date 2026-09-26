using ClaimsModule.API.Middleware;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClaimsModule.API.Swagger;

public class IdempotencyKeyHeaderFilter : IOperationFilter
{
    private static readonly string[] WriteMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!WriteMethods.Contains(context.ApiDescription.HttpMethod, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional. Repeating a request with the same key replays the original successful response instead of executing it again.",
            Schema = new OpenApiSchema { Type = "string", MaxLength = IdempotencyMiddleware.KeyMaxLength }
        });
    }
}
