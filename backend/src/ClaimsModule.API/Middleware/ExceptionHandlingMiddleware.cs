using System.Net;
using System.Text.Json;
using FluentValidation;

namespace ClaimsModule.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteValidationErrorAsync(context, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteUnexpectedErrorAsync(context);
        }
    }

    private static async Task WriteValidationErrorAsync(HttpContext context, ValidationException ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;

        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var body = new
        {
            type = "ValidationError",
            title = "One or more validation errors occurred.",
            status = 422,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }

    private static async Task WriteUnexpectedErrorAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var body = new
        {
            type = "ServerError",
            title = "An unexpected error occurred.",
            status = 500
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
