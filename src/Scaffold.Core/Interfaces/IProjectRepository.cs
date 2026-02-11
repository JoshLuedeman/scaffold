using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IProjectRepository
{
    Task<MigrationProject> GetByIdAsync(Guid id);
    Task<IReadOnlyList<MigrationProject>> GetAllAsync();
    Task<MigrationProject> CreateAsync(MigrationProject project);
    Task UpdateAsync(MigrationProject project);
    Task DeleteAsync(Guid id);
}
