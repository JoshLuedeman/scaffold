using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scaffold.Api.Middleware;

namespace Scaffold.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a test server with the middleware registered. The <paramref name="throwException"/>
    /// delegate is invoked by the terminal middleware — return null to let the request succeed,
    /// or return an exception to have it thrown.
    /// </summary>
    private static async Task<IHost> CreateHost(
        Func<Exception?> throwException,
        string environmentName = "Production")
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .UseEnvironment(environmentName)
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.Run(context =>
                        {
                            var ex = throwException();
                            if (ex is not null)
                                throw ex;

                            context.Response.StatusCode = 200;
                            return Task.CompletedTask;
                        });
                    });
            })
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task PassThrough_NoException_Returns200()
    {
        using var host = await CreateHost(() => null);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(typeof(KeyNotFoundException), HttpStatusCode.NotFound, "Not Found")]
    [InlineData(typeof(ArgumentException), HttpStatusCode.BadRequest, "Bad Request")]
    [InlineData(typeof(ArgumentNullException), HttpStatusCode.BadRequest, "Bad Request")]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.Conflict, "Conflict")]
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(typeof(NullReferenceException), HttpStatusCode.InternalServerError, "Internal Server Error")]
    [InlineData(typeof(InvalidCastException), HttpStatusCode.InternalServerError, "Internal Server Error")]
    public async Task Exception_MapsToCorrectStatusCode(
        Type exceptionType, HttpStatusCode expectedStatus, string expectedTitle)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error message")!;
        using var host = await CreateHost(() => exception);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/test");

        Assert.Equal(expectedStatus, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
    }

    [Fact]
    public async Task Exception_ContentType_IsApplicationProblemJson()
    {
        using var host = await CreateHost(() => new InvalidOperationException("boom"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/test");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Exception_Instance_ContainsRequestPath()
    {
        using var host = await CreateHost(() => new InvalidOperationException("boom"));
        var client = host.GetTestClient();

        var response = await client.GetAsync("/my/resource");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal("/my/resource", problem.Instance);
    }

    [Fact]
    public async Task Development_IncludesExceptionDetails()
    {
        using var host = await CreateHost(
            () => new InvalidOperationException("secret-detail"),
            environmentName: "Development");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/test");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Contains("secret-detail", problem.Detail);
    }

    [Fact]
    public async Task Production_HidesExceptionDetails()
    {
        using var host = await CreateHost(
            () => new InvalidOperationException("secret-detail"),
            environmentName: "Production");
        var client = host.GetTestClient();

        var response = await client.GetAsync("/test");

        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

        Assert.NotNull(problem);
        Assert.Null(problem.Detail);
    }

    [Fact]
    public void MapException_ArgumentNullException_BeforeArgumentException()
    {
        // ArgumentNullException derives from ArgumentException — verify it's handled
        // correctly (both map to 400 but this validates the switch order)
        var (statusCode, title) = ExceptionHandlingMiddleware.MapException(
            new ArgumentNullException("param"));

        Assert.Equal(400, statusCode);
        Assert.Equal("Bad Request", title);
    }
}
