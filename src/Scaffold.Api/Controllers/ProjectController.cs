using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;

    public ProjectController(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectRepository.GetAllAsync();
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(id);
            return Ok(project);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var project = new MigrationProject
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            SourceConnection = request.ConnectionInfo,
            CreatedBy = User.Identity?.Name ?? "unknown"
        };

        var created = await _projectRepository.CreateAsync(project);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(id);
            project.Name = request.Name ?? project.Name;
            project.Description = request.Description ?? project.Description;
            await _projectRepository.UpdateAsync(project);
            return Ok(project);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _projectRepository.GetByIdAsync(id);
            await _projectRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record CreateProjectRequest(
    [Required][StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Description,
    Core.Models.ConnectionInfo? ConnectionInfo);
public record UpdateProjectRequest(
    [StringLength(200)] string? Name,
    [StringLength(2000)] string? Description);
