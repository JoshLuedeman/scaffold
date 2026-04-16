using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly ScaffoldDbContext _context;

    public ProjectRepository(ScaffoldDbContext context)
    {
        _context = context;
    }

    public async Task<MigrationProject> GetByIdAsync(Guid id)
    {
        var project = await _context.MigrationProjects
            .Include(p => p.SourceConnection)
            .Include(p => p.Assessment)
            .Include(p => p.MigrationPlan)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project ?? throw new KeyNotFoundException($"Project with ID {id} not found.");
    }

    public async Task<IReadOnlyList<MigrationProject>> GetAllAsync()
    {
        return await _context.MigrationProjects
            .Include(p => p.SourceConnection)
            .Include(p => p.Assessment)
            .Include(p => p.MigrationPlan)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PaginatedResult<MigrationProject>> GetAllAsync(int page, int pageSize)
    {
        var totalCount = await _context.MigrationProjects.CountAsync();

        var items = await _context.MigrationProjects
            .Include(p => p.SourceConnection)
            .Include(p => p.Assessment)
            .Include(p => p.MigrationPlan)
            .AsNoTracking()
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<MigrationProject>(items, totalCount, page, pageSize);
    }

    public async Task<MigrationProject> CreateAsync(MigrationProject project)
    {
        _context.MigrationProjects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task UpdateAsync(MigrationProject project)
    {
        project.UpdatedAt = DateTime.UtcNow;
        _context.MigrationProjects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _context.MigrationProjects.FindAsync(id);
        if (project is not null)
        {
            _context.MigrationProjects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }
}
