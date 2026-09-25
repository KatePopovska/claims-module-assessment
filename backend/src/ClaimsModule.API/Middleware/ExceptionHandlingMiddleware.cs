using System.Net;
using System.Text.Json;
using ClaimsModule.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

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
        catch (NotFoundException ex)
        {
            await WriteNotFoundErrorAsync(context, ex);
        }
        catch (DbUpdateException ex) when (IsConcurrencyConflict(ex))
        {
            logger.LogWarning(ex, "Concurrency conflict processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteConflictErrorAsync(context);
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

    private static async Task WriteNotFoundErrorAsync(HttpContext context, NotFoundException ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;

        var body = new
        {
            type = "NotFound",
            title = ex.Message,
            status = 404
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }

    private static bool IsConcurrencyConflict(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException
        || ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };

    private static async Task WriteConflictErrorAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        var body = new
        {
            type = "Conflict",
            title = "The claim was changed by another request. Reload it and try again.",
            status = 409
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
