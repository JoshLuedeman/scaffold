using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Scaffold.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions from the request pipeline and returns
/// RFC 7807 ProblemDetails responses with appropriate HTTP status codes.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = context.Request.Path
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Detail = exception.ToString();
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }

    internal static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        KeyNotFoundException => ((int)HttpStatusCode.NotFound, "Not Found"),
        ArgumentNullException => ((int)HttpStatusCode.BadRequest, "Bad Request"),
        ArgumentException => ((int)HttpStatusCode.BadRequest, "Bad Request"),
        InvalidOperationException => ((int)HttpStatusCode.Conflict, "Conflict"),
        UnauthorizedAccessException => ((int)HttpStatusCode.Forbidden, "Forbidden"),
        _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error")
    };
}
