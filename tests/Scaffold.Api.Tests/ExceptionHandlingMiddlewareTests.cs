using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Scaffold.Api.Middleware;

namespace Scaffold.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        var logger = new LoggerFactory().CreateLogger<ExceptionHandlingMiddleware>();
        return new ExceptionHandlingMiddleware(next, logger);
    }

    private static DefaultHttpContext CreateHttpContext(bool isDevelopment = false)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(isDevelopment));
        context.RequestServices = services.BuildServiceProvider();

        return context;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(response.Body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — response should not be modified by the middleware
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("Item not found"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Resource not found", problem.Title);
        Assert.Equal("Item not found", problem.Detail);
        Assert.Equal("/api/test", problem.Instance);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ArgumentException("Bad argument"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Invalid request", problem.Title);
        Assert.Equal("Bad argument", problem.Detail);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentNullException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ArgumentNullException("param", "Param is required"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Invalid request", problem.Title);
    }

    [Fact]
    public async Task InvokeAsync_InvalidOperationException_Returns409()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Conflict occurred"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Operation conflict", problem.Title);
        Assert.Equal("Conflict occurred", problem.Detail);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns403()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("Not allowed"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(403, problem.Status);
        Assert.Equal("Forbidden", problem.Title);
        Assert.Equal("Not allowed", problem.Detail);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new NullReferenceException("Something broke"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.Equal("An unexpected error occurred", problem.Title);
        Assert.Equal("Something broke", problem.Detail);
    }

    [Fact]
    public async Task InvokeAsync_Exception_ResponseContentType_IsProblemJson()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new Exception("Error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_ProductionEnvironment_DoesNotExposeExceptionDetails()
    {
        // Arrange — isDevelopment: false simulates Production
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("Sensitive info about DB"));
        var context = CreateHttpContext(isDevelopment: false);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Null(problem.Detail); // No exception details in Production
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_IncludesExceptionDetails()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("Detailed error info"));
        var context = CreateHttpContext(isDevelopment: true);

        // Act
        await middleware.InvokeAsync(context);
        var problem = await ReadProblemDetails(context.Response);

        // Assert
        Assert.NotNull(problem);
        Assert.Equal("Detailed error info", problem.Detail);
    }

    [Theory]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(ArgumentNullException), 400)]
    [InlineData(typeof(InvalidOperationException), 409)]
    [InlineData(typeof(UnauthorizedAccessException), 403)]
    [InlineData(typeof(NullReferenceException), 500)]
    public async Task InvokeAsync_ExceptionMapping_ReturnsCorrectStatusCode(Type exceptionType, int expectedStatusCode)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test message")!;
        var middleware = CreateMiddleware(_ => throw exception);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    }

    /// <summary>
    /// Minimal IWebHostEnvironment stub for unit testing the middleware.
    /// </summary>
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(bool isDevelopment = false)
        {
            EnvironmentName = isDevelopment ? "Development" : "Production";
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Scaffold.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
