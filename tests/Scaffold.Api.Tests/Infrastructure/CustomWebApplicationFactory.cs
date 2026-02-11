using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            ReplaceDbContext(services, _dbName);
            ReplaceExternalServices(services);

            // Override authentication to use the test handler that auto-authenticates
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }

    internal static void ReplaceDbContext(IServiceCollection services, string dbName)
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<ScaffoldDbContext>));
        if (descriptor != null) services.Remove(descriptor);

        var dbContextDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(ScaffoldDbContext));
        if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

        services.AddDbContext<ScaffoldDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
    }

    internal static void ReplaceExternalServices(IServiceCollection services)
    {
        var migrationEngineDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IMigrationEngine));
        if (migrationEngineDescriptor != null) services.Remove(migrationEngineDescriptor);
        services.AddScoped<IMigrationEngine, StubMigrationEngine>();

        var validationDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(ValidationEngine));
        if (validationDescriptor != null) services.Remove(validationDescriptor);
        services.AddSingleton<ValidationEngine>();
    }
}

/// <summary>
/// Factory variant that rejects all auth — used to test 401 responses.
/// </summary>
public class UnauthenticatedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            CustomWebApplicationFactory.ReplaceDbContext(services, _dbName);
            CustomWebApplicationFactory.ReplaceExternalServices(services);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, RejectAllAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>
/// Auth handler that always fails — used to test 401 responses.
/// </summary>
public class RejectAllAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public RejectAllAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.Fail("No valid credentials"));
}

/// <summary>
/// Stub IMigrationEngine that doesn't connect to any real database.
/// </summary>
public class StubMigrationEngine : IMigrationEngine
{
    public string SourcePlatform => "Stub";

    public Task<MigrationResult> ExecuteCutoverAsync(
        MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default)
    {
        return Task.FromResult(new MigrationResult
        {
            Id = Guid.NewGuid(),
            ProjectId = plan.ProjectId,
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    public Task StartContinuousSyncAsync(
        MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<MigrationResult> CompleteCutoverAsync(Guid migrationId, CancellationToken ct = default)
    {
        return Task.FromResult(new MigrationResult
        {
            Id = migrationId,
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }
}
