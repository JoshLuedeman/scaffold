using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Controllers;
using Scaffold.Api.Tests.Infrastructure;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

/// <summary>
/// Tests that controllers correctly resolve engines via factories (not direct injection).
/// Validates the DI refactor from IAssessmentEngine/IMigrationEngine to factories.
/// </summary>
public class EngineFactoryDiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EngineFactoryDiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void DI_Resolves_IAssessmentEngineFactory()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IAssessmentEngineFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public void DI_Resolves_IMigrationEngineFactory()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IMigrationEngineFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public void DI_Does_Not_Register_IAssessmentEngine_Directly()
    {
        // After the refactor, IAssessmentEngine should not be registered as a service.
        // Controllers use IAssessmentEngineFactory instead.
        using var scope = _factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetService<IAssessmentEngine>();

        Assert.Null(engine);
    }

    [Fact]
    public void DI_Does_Not_Register_IMigrationEngine_Directly()
    {
        // After the refactor, IMigrationEngine should not be registered as a service.
        // Controllers use IMigrationEngineFactory instead.
        using var scope = _factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetService<IMigrationEngine>();

        Assert.Null(engine);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer)]
    public void Factory_Creates_AssessmentEngine_For_Platform(DatabasePlatform platform)
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IAssessmentEngineFactory>();

        var engine = factory.Create(platform);

        Assert.NotNull(engine);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer)]
    public void Factory_Creates_MigrationEngine_For_Platform(DatabasePlatform platform)
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IMigrationEngineFactory>();

        var engine = factory.Create(platform);

        Assert.NotNull(engine);
    }

    [Fact]
    public async Task Assessment_Endpoint_Uses_Factory_To_Run_Assessment()
    {
        // Arrange — create a project with a source connection that has a specific platform
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = $"FactoryDI-{Guid.NewGuid()}",
            CreatedBy = "test",
            SourceConnection = new ConnectionInfo
            {
                Id = Guid.NewGuid(),
                Server = "localhost",
                Database = "TestDb",
                Port = 1433,
                UseSqlAuthentication = true,
                Username = "sa",
                TrustServerCertificate = true,
                Platform = DatabasePlatform.SqlServer
            }
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            db.MigrationProjects.Add(project);
            await db.SaveChangesAsync();
        }

        // Act — start an assessment (the controller must use the factory, not a direct engine)
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/assessments",
            new AssessmentRequest(null, null, null, null, null, "test", null, null));

        // Assert — the stub factory returns a stub engine that produces a valid report
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<AssessmentReport>(JsonOptions);
        Assert.NotNull(report);
        Assert.Equal(project.Id, report!.ProjectId);
        Assert.True(report.Schema.TableCount > 0, "Stub engine should produce schema data");
    }

    [Fact]
    public async Task Connection_Test_Endpoint_Uses_Factory()
    {
        // Act — test a connection (the controller resolves the engine via factory + platform)
        var response = await _client.PostAsJsonAsync("/api/connections/test", new
        {
            Server = "localhost",
            Database = "TestDb",
            Port = 1433,
            UseSqlAuthentication = false,
            TrustServerCertificate = true,
            Platform = "SqlServer"
        });

        // Assert — the stub factory returns a stub engine that reports success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean(),
            "Stub engine should report successful connection test");
    }
}
