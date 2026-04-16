using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Scaffold.Api.Hubs;
using Scaffold.Api.Middleware;
using Scaffold.Api.Services;
using Scaffold.Assessment.Pricing;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure;
using Scaffold.Infrastructure.Data;
using Scaffold.Migration.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Application Insights telemetry (connection string picked up from APPLICATIONINSIGHTS_CONNECTION_STRING env var)
builder.Services.AddApplicationInsightsTelemetry();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddInfrastructure(connectionString);

var disableAuth = builder.Configuration.GetValue<bool>("DisableAuth")
    && builder.Environment.IsDevelopment();
if (disableAuth)
{
    builder.Logging.AddConsole();
    var startupLogger = LoggerFactory.Create(lb => lb.AddConsole()).CreateLogger("Startup");
    startupLogger.LogWarning("*** Authentication is DISABLED (DevAuthHandler). Do NOT use in production. ***");
    builder.Services.AddAuthentication()
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Scaffold.Api.DevAuthHandler>(
            "Bearer", _ => { });
    builder.Services.AddAuthorization(options =>
        options.FallbackPolicy = null);
}
else
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
    builder.Services.AddAuthorization();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOrigin", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddScoped<MigrationProgressService>();
builder.Services.AddHostedService<MigrationSchedulerService>();

builder.Services.AddDataProtection();
builder.Services.AddScoped<IConnectionStringProtector, Scaffold.Infrastructure.Security.ConnectionStringProtector>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<AzurePricingService>();
builder.Services.AddScoped<IAzurePricingService, AzurePricingService>();

builder.Services.AddSingleton<SqlServerConnectionFactory>();
builder.Services.AddScoped<SqlServerAssessor>();
builder.Services.AddScoped<SqlServerMigrator>();
builder.Services.AddScoped<IAssessmentEngineFactory, AssessmentEngineFactory>();
builder.Services.AddScoped<IMigrationEngineFactory, MigrationEngineFactory>();
builder.Services.AddSingleton<ValidationEngine>();
builder.Services.AddScoped<IPreMigrationValidator, PreMigrationValidator>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ScaffoldDbContext>();

var app = builder.Build();

// Apply pending EF Core migrations on startup (skip for in-memory test databases)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

// Configure the HTTP request pipeline.

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("FrontendOrigin");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapHub<MigrationHub>("/hubs/migration");

app.Run();

// Make Program accessible to integration tests
public partial class Program { }
