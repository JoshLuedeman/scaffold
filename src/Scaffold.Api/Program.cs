using Microsoft.Identity.Web;
using Scaffold.Api.Hubs;
using Scaffold.Api.Services;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure;
using Scaffold.Migration.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

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

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<MigrationProgressService>();

builder.Services.AddSingleton<SqlServerConnectionFactory>();
builder.Services.AddScoped<IAssessmentEngine, SqlServerAssessor>();
builder.Services.AddScoped<IMigrationEngine, SqlServerMigrator>();
builder.Services.AddSingleton<ValidationEngine>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseCors("FrontendOrigin");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MigrationHub>("/hubs/migration");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
}).RequireAuthorization();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program accessible to integration tests
public partial class Program { }
