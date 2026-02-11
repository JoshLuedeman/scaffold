using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;
using Scaffold.Infrastructure.Repositories;

namespace Scaffold.Api.Tests.Infrastructure;

public class ProjectRepositoryTests : IDisposable
{
    private readonly ScaffoldDbContext _context;
    private readonly ProjectRepository _repository;

    public ProjectRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ScaffoldDbContext(options);
        _repository = new ProjectRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private static MigrationProject CreateProject(string name = "Test Project") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Test description",
        Status = ProjectStatus.Created,
        CreatedBy = "testuser"
    };

    [Fact]
    public async Task CreateAsync_ReturnsProjectWithGeneratedId()
    {
        var project = new MigrationProject { Name = "New Project", CreatedBy = "user" };

        var result = await _repository.CreateAsync(project);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("New Project", result.Name);
        Assert.Equal("user", result.CreatedBy);

        var persisted = await _context.MigrationProjects.FindAsync(result.Id);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectProject()
    {
        var project = CreateProject("Find Me");
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _repository.GetByIdAsync(project.Id);

        Assert.Equal(project.Id, result.Id);
        Assert.Equal("Find Me", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesRelatedEntities()
    {
        var project = CreateProject("With Relations");
        project.SourceConnection = new ConnectionInfo
        {
            Id = Guid.NewGuid(),
            Server = "server",
            Database = "db"
        };
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _repository.GetByIdAsync(project.Id);

        Assert.NotNull(result.SourceConnection);
        Assert.Equal("server", result.SourceConnection.Server);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProjects()
    {
        _context.MigrationProjects.AddRange(
            CreateProject("Project 1"),
            CreateProject("Project 2"),
            CreateProject("Project 3"));
        await _context.SaveChangesAsync();

        var results = await _repository.GetAllAsync();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var project = CreateProject();
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();

        project.Name = "Updated Name";
        project.Status = ProjectStatus.Assessing;
        await _repository.UpdateAsync(project);

        _context.ChangeTracker.Clear();
        var updated = await _context.MigrationProjects.FindAsync(project.Id);

        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(ProjectStatus.Assessing, updated.Status);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAtTimestamp()
    {
        var project = CreateProject();
        var originalUpdatedAt = project.UpdatedAt;
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();

        await Task.Delay(10); // Ensure time difference
        await _repository.UpdateAsync(project);

        Assert.True(project.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProject()
    {
        var project = CreateProject();
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();

        await _repository.DeleteAsync(project.Id);

        var deleted = await _context.MigrationProjects.FindAsync(project.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_NonexistentId_DoesNotThrow()
    {
        await _repository.DeleteAsync(Guid.NewGuid()); // should not throw
    }

    [Fact]
    public async Task GetByIdAsync_NonexistentId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _repository.GetByIdAsync(Guid.NewGuid()));
    }
}
